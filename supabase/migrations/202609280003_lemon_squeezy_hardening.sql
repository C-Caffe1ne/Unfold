begin;

alter table public.orders
    add constraint checkout_reference_unique unique (provider, environment, provider_checkout_id);

create or replace function public.apply_lemon_event(p_order_id uuid, p_event_key text, p_provider_order_id text,
    p_kind text, p_currency text, p_subtotal integer, p_tax integer, p_total integer,
    p_refunded_total integer default 0)
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
        or p_subtotal is null or p_subtotal <> target.amount_minor
        or p_tax is null or p_tax < 0 or p_total is null or p_total <= 0
        or p_total::bigint <> p_subtotal::bigint + p_tax
        or p_refunded_total is null or p_refunded_total < 0 or p_refunded_total > p_total
        or (p_kind = 'paid' and p_refunded_total <> 0)
        then raise exception 'Lemon event does not match order'; end if;
    select * into receipt from public.lemon_events where environment = 'test' and event_key = p_event_key;
    if found then
        if (receipt.order_id,receipt.provider_order_id,receipt.kind,receipt.subtotal_minor,receipt.tax_minor,
            receipt.total_minor,receipt.refunded_total_minor)
            is distinct from (p_order_id,p_provider_order_id,p_kind,p_subtotal,p_tax,p_total,p_refunded_total)
            then raise exception 'Lemon event conflict'; end if;
        return false;
    end if;
    if target.total_minor is not null and (target.total_minor <> p_total or target.tax_minor <> p_tax)
        then raise exception 'Lemon totals conflict'; end if;
    update public.orders set provider_order_id = p_provider_order_id, tax_minor = p_tax, total_minor = p_total,
        refunded_total_minor = greatest(refunded_total_minor, p_refunded_total), updated_at = now()
        where id = p_order_id returning * into target;
    insert into public.lemon_events values ('test',p_event_key,p_order_id,p_provider_order_id,p_kind,
        p_subtotal,p_tax,p_total,p_refunded_total,now());
    if target.refunded_total_minor = target.total_minor then
        perform public.apply_verified_payment(p_order_id,'lemon','test',p_event_key,p_provider_order_id,'refunded',p_currency,target.amount_minor);
    elsif p_kind = 'paid' then
        perform public.apply_verified_payment(p_order_id,'lemon','test',p_event_key,p_provider_order_id,'paid',p_currency,target.amount_minor);
    end if;
    return true;
end;
$$;

revoke all on function public.apply_lemon_event(uuid,text,text,text,text,integer,integer,integer,integer)
    from public, anon, authenticated;
grant execute on function public.apply_lemon_event(uuid,text,text,text,text,integer,integer,integer,integer)
    to service_role;

commit;
