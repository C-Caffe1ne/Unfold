begin;

alter table public.lemon_events drop constraint if exists lemon_events_check;
alter table public.lemon_events add constraint lemon_events_check check (total_minor = subtotal_minor + tax_minor);

-- Live catalog is deliberately not populated or enabled by this migration.
alter table public.lemon_prices drop constraint lemon_prices_environment_check;
alter table public.lemon_prices add constraint lemon_prices_environment_check check (environment in ('test','live'));
alter table public.lemon_events drop constraint lemon_events_environment_check;
alter table public.lemon_events add constraint lemon_events_environment_check check (environment in ('test','live'));

create or replace function public.reserve_lemon_checkout(p_user_id uuid, p_price_id text, p_request_id uuid, p_environment text)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    price public.prices%rowtype;
    mapping public.lemon_prices%rowtype;
begin
    if p_environment is null or p_environment not in ('test','live') then raise exception 'Invalid payment environment'; end if;
    select * into strict price from public.prices where id = p_price_id and product_id = 'unfold';
    if p_environment = 'live' and not price.live_enabled then raise exception 'Lemon catalog not configured'; end if;
    if p_request_id is null then raise exception 'Invalid checkout request'; end if;
    perform pg_advisory_xact_lock(hashtextextended(p_user_id::text || '/unfold/' || p_environment, 0));
    if exists (select 1 from public.entitlements where user_id = p_user_id and product_id = 'unfold'
        and environment = p_environment and status = 'active') then raise exception 'Already purchased'; end if;
    select * into target from public.orders where user_id = p_user_id and provider = 'lemon'
        and environment = p_environment and request_id = p_request_id;
    if found then
        if target.price_id <> p_price_id or target.status <> 'pending' then raise exception 'Checkout request conflict'; end if;
        if target.checkout_state = 'retryable' then
            update public.orders set checkout_state = 'creating', updated_at = now() where id = target.id returning * into target;
            return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
        end if;
        return jsonb_build_object('order', to_jsonb(target), 'acquired', false);
    end if;
    select * into target from public.orders where user_id = p_user_id and provider = 'lemon'
        and environment = p_environment and status = 'pending' and price_id = p_price_id
        and (checkout_state in ('retryable', 'creating', 'uncertain') or created_at > now() - interval '30 minutes')
        order by created_at desc limit 1;
    if found then
        if target.checkout_state = 'retryable' then
            update public.orders set checkout_state = 'creating', updated_at = now() where id = target.id returning * into target;
            return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
        end if;
        return jsonb_build_object('order', to_jsonb(target), 'acquired', false);
    end if;
    if (select count(*) from public.orders where user_id = p_user_id and provider = 'lemon'
        and created_at > now() - interval '10 minutes') >= 5 then raise exception 'Checkout rate limited'; end if;
    select * into mapping from public.lemon_prices where price_id = p_price_id and environment = p_environment;
    if not found then raise exception 'Lemon catalog not configured'; end if;
    insert into public.orders(user_id, product_id, price_id, provider, environment, currency, amount_minor,
        request_id, provider_price_id, provider_product_id, provider_store_id, provider_checkout_host, checkout_state)
        values (p_user_id, price.product_id, price.id, 'lemon', p_environment, price.currency, price.amount_minor,
            p_request_id, mapping.variant_id, mapping.product_id, mapping.store_id, mapping.checkout_host, 'creating')
        returning * into target;
    return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
end;
$$;

create or replace function public.reserve_lemon_checkout(p_user_id uuid, p_price_id text, p_request_id uuid)
returns jsonb language sql security definer set search_path = '' as $$
    select public.reserve_lemon_checkout(p_user_id, p_price_id, p_request_id, 'test');
$$;

create or replace function public.bind_lemon_checkout(p_order_id uuid, p_checkout_id text)
returns void language plpgsql security definer set search_path = '' as $$
declare target public.orders%rowtype;
begin
    if p_checkout_id is null or p_checkout_id !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
        then raise exception 'Invalid checkout'; end if;
    select * into strict target from public.orders where id = p_order_id for update;
    if target.provider <> 'lemon' or target.environment not in ('test','live') or target.status <> 'pending'
        or (target.provider_checkout_id is not null and target.provider_checkout_id <> p_checkout_id)
        then raise exception 'Checkout binding conflict'; end if;
    update public.orders set provider_checkout_id = p_checkout_id, checkout_state = 'ready', updated_at = now()
        where id = p_order_id;
end;
$$;

create or replace function public.mark_lemon_checkout_uncertain(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'uncertain', updated_at = now()
    where id = p_order_id and provider = 'lemon' and environment in ('test','live') and provider_checkout_id is null;
$$;

create or replace function public.release_lemon_checkout(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'retryable', updated_at = now()
    where id = p_order_id and provider = 'lemon' and environment in ('test','live')
        and checkout_state = 'creating' and provider_checkout_id is null;
$$;

create or replace function public.apply_lemon_event(p_order_id uuid, p_event_key text, p_provider_order_id text,
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
    if target.provider <> 'lemon' or target.environment not in ('test','live')
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
    select * into receipt from public.lemon_events where environment = target.environment and event_key = p_event_key;
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
        values (target.environment,p_event_key,p_order_id,p_provider_order_id,p_kind,p_item_price,
            p_subtotal,p_tax,p_total,p_refunded_total,now());
    if target.refunded_total_minor = target.total_minor then
        perform public.apply_verified_payment(p_order_id,'lemon',target.environment,p_event_key,p_provider_order_id,'refunded',p_currency,target.amount_minor);
    elsif p_kind = 'paid' then
        perform public.apply_verified_payment(p_order_id,'lemon',target.environment,p_event_key,p_provider_order_id,'paid',p_currency,target.amount_minor);
    end if;
    return true;
end;
$$;

revoke all on function public.reserve_lemon_checkout(uuid,text,uuid,text) from public, anon, authenticated;
grant execute on function public.reserve_lemon_checkout(uuid,text,uuid,text) to service_role;

commit;
