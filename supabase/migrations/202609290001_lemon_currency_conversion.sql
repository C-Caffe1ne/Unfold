begin;

-- Lemon charges in USD and can report converted order amounts with fractional units.
-- Keep the contracted item price separate from the converted charged subtotal.
alter table public.orders drop constraint if exists order_total_matches;
alter table public.orders
    alter column tax_minor type numeric(20,8) using tax_minor::numeric,
    alter column total_minor type numeric(20,8) using total_minor::numeric,
    alter column refunded_total_minor type numeric(20,8) using refunded_total_minor::numeric;

alter table public.lemon_events
    alter column subtotal_minor type numeric(20,8) using subtotal_minor::numeric,
    alter column tax_minor type numeric(20,8) using tax_minor::numeric,
    alter column total_minor type numeric(20,8) using total_minor::numeric,
    alter column refunded_total_minor type numeric(20,8) using refunded_total_minor::numeric,
    add column item_price_minor numeric(20,8);
update public.lemon_events set item_price_minor = subtotal_minor where item_price_minor is null;
alter table public.lemon_events alter column item_price_minor set not null;

drop function public.apply_lemon_event(uuid,text,text,text,text,integer,integer,integer,integer);

create function public.apply_lemon_event(p_order_id uuid, p_event_key text, p_provider_order_id text,
    p_kind text, p_currency text, p_item_price numeric, p_subtotal numeric, p_tax numeric, p_total numeric,
    p_refunded_total numeric default 0)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    receipt public.lemon_events%rowtype;
begin
    select * into strict target from public.orders where id = p_order_id;
    perform pg_advisory_xact_lock(hashtextextended(target.user_id::text || '/' || target.product_id || '/' || target.environment, 0));
    select * into strict target from public.orders where id = p_order_id for update;
    if target.provider <> 'lemon' or target.environment <> 'test'
        or target.provider_checkout_id is null or target.checkout_state <> 'ready'
        or p_provider_order_id is null or p_provider_order_id !~ '^[1-9][0-9]{0,18}$'
        or (target.provider_order_id is not null and target.provider_order_id <> p_provider_order_id)
        or p_event_key is null or length(p_event_key) not between 1 and 256
        or p_kind is null or p_kind not in ('paid', 'refund')
        or p_currency is distinct from target.currency
        or p_item_price is null or p_item_price <> target.amount_minor
        or p_subtotal is null or p_subtotal <= 0
        or p_tax is null or p_tax < 0 or p_total is null or p_total <= 0
        or p_total <> p_subtotal + p_tax
        or p_refunded_total is null or p_refunded_total < 0 or p_refunded_total > p_total
        or (p_kind = 'paid' and p_refunded_total <> 0)
        then raise exception 'Lemon event does not match order'; end if;
    select * into receipt from public.lemon_events where environment = 'test' and event_key = p_event_key;
    if found then
        if (receipt.order_id,receipt.provider_order_id,receipt.kind,receipt.item_price_minor,
            receipt.subtotal_minor,receipt.tax_minor,receipt.total_minor,receipt.refunded_total_minor)
            is distinct from (p_order_id,p_provider_order_id,p_kind,p_item_price,
                p_subtotal,p_tax,p_total,p_refunded_total)
            then raise exception 'Lemon event conflict'; end if;
        return false;
    end if;
    if target.total_minor is not null and (target.total_minor <> p_total or target.tax_minor <> p_tax)
        then raise exception 'Lemon totals conflict'; end if;
    update public.orders set provider_order_id = p_provider_order_id, tax_minor = p_tax, total_minor = p_total,
        refunded_total_minor = greatest(refunded_total_minor, p_refunded_total), updated_at = now()
        where id = p_order_id returning * into target;
    insert into public.lemon_events(environment,event_key,order_id,provider_order_id,kind,item_price_minor,
        subtotal_minor,tax_minor,total_minor,refunded_total_minor,processed_at)
        values ('test',p_event_key,p_order_id,p_provider_order_id,p_kind,p_item_price,
            p_subtotal,p_tax,p_total,p_refunded_total,now());
    if target.refunded_total_minor = target.total_minor then
        perform public.apply_verified_payment(p_order_id,'lemon','test',p_event_key,p_provider_order_id,'refunded',p_currency,target.amount_minor);
    elsif p_kind = 'paid' then
        perform public.apply_verified_payment(p_order_id,'lemon','test',p_event_key,p_provider_order_id,'paid',p_currency,target.amount_minor);
    end if;
    return true;
end;
$$;

revoke all on function public.apply_lemon_event(uuid,text,text,text,text,numeric,numeric,numeric,numeric,numeric)
    from public, anon, authenticated;
grant execute on function public.apply_lemon_event(uuid,text,text,text,text,numeric,numeric,numeric,numeric,numeric)
    to service_role;

comment on column public.lemon_events.item_price_minor is 'Contracted item price in Unfold internal minor units';
comment on column public.lemon_events.subtotal_minor is 'Lemon charged subtotal converted to Unfold internal units; may be fractional';

commit;
