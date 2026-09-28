begin;

-- Paddle remains as historical test data. New checkout traffic is reserved for Lemon Squeezy.
alter table public.orders
    add column provider_checkout_id text,
    add column provider_store_id text,
    add column provider_checkout_host text,
    add column refunded_total_minor integer not null default 0 check (refunded_total_minor >= 0);

create table public.lemon_prices (
    price_id text not null references public.prices(id),
    environment text not null check (environment = 'test'),
    store_id text not null check (store_id ~ '^[1-9][0-9]{0,18}$'),
    variant_id text not null check (variant_id ~ '^[1-9][0-9]{0,18}$'),
    product_id text not null check (product_id ~ '^[1-9][0-9]{0,18}$'),
    checkout_host text not null check (checkout_host ~ '^[a-z0-9][a-z0-9.-]*\.lemonsqueezy\.com$'),
    primary key (price_id, environment),
    unique (environment, store_id, variant_id)
);

create table public.lemon_events (
    environment text not null check (environment = 'test'),
    event_key text not null check (length(event_key) between 1 and 256),
    order_id uuid not null references public.orders(id),
    provider_order_id text not null check (provider_order_id ~ '^[1-9][0-9]{0,18}$'),
    kind text not null check (kind in ('paid', 'refund')),
    subtotal_minor integer not null check (subtotal_minor > 0),
    tax_minor integer not null check (tax_minor >= 0),
    total_minor integer not null check (total_minor > 0),
    refunded_total_minor integer not null check (refunded_total_minor >= 0),
    processed_at timestamptz not null default now(),
    primary key (environment, event_key),
    check (total_minor::bigint = subtotal_minor::bigint + tax_minor),
    check (refunded_total_minor <= total_minor)
);

alter table public.lemon_prices enable row level security;
alter table public.lemon_events enable row level security;
revoke all on public.lemon_prices, public.lemon_events from public, anon, authenticated;
grant all on public.lemon_prices, public.lemon_events to service_role;

create function public.reserve_lemon_checkout(p_user_id uuid, p_price_id text, p_request_id uuid)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    price public.prices%rowtype;
    mapping public.lemon_prices%rowtype;
begin
    if p_request_id is null then raise exception 'Invalid checkout request'; end if;
    perform pg_advisory_xact_lock(hashtextextended(p_user_id::text || '/unfold/test', 0));
    if exists (select 1 from public.entitlements where user_id = p_user_id and product_id = 'unfold'
        and environment = 'test' and status = 'active') then raise exception 'Already purchased'; end if;
    select * into target from public.orders where user_id = p_user_id and provider = 'lemon'
        and environment = 'test' and request_id = p_request_id;
    if found then
        if target.price_id <> p_price_id or target.status <> 'pending' then raise exception 'Checkout request conflict'; end if;
        if target.checkout_state = 'retryable' then
            update public.orders set checkout_state = 'creating', updated_at = now() where id = target.id returning * into target;
            return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
        end if;
        return jsonb_build_object('order', to_jsonb(target), 'acquired', false);
    end if;
    select * into target from public.orders where user_id = p_user_id and provider = 'lemon'
        and environment = 'test' and status = 'pending' and price_id = p_price_id
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
    select * into strict price from public.prices where id = p_price_id and product_id = 'unfold';
    select * into mapping from public.lemon_prices where price_id = p_price_id and environment = 'test';
    if not found then raise exception 'Lemon catalog not configured'; end if;
    insert into public.orders(user_id, product_id, price_id, provider, environment, currency, amount_minor,
        request_id, provider_price_id, provider_product_id, provider_store_id, provider_checkout_host, checkout_state)
        values (p_user_id, price.product_id, price.id, 'lemon', 'test', price.currency, price.amount_minor,
            p_request_id, mapping.variant_id, mapping.product_id, mapping.store_id, mapping.checkout_host, 'creating')
        returning * into target;
    return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
end;
$$;

create function public.bind_lemon_checkout(p_order_id uuid, p_checkout_id text)
returns void language plpgsql security definer set search_path = '' as $$
declare target public.orders%rowtype;
begin
    if p_checkout_id is null or p_checkout_id !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
        then raise exception 'Invalid checkout'; end if;
    select * into strict target from public.orders where id = p_order_id for update;
    if target.provider <> 'lemon' or target.environment <> 'test' or target.status <> 'pending'
        or (target.provider_checkout_id is not null and target.provider_checkout_id <> p_checkout_id)
        then raise exception 'Checkout binding conflict'; end if;
    update public.orders set provider_checkout_id = p_checkout_id, checkout_state = 'ready', updated_at = now()
        where id = p_order_id;
end;
$$;

create function public.mark_lemon_checkout_uncertain(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'uncertain', updated_at = now()
    where id = p_order_id and provider = 'lemon' and environment = 'test' and provider_checkout_id is null;
$$;

create function public.release_lemon_checkout(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'retryable', updated_at = now()
    where id = p_order_id and provider = 'lemon' and environment = 'test'
        and checkout_state = 'creating' and provider_checkout_id is null;
$$;

create function public.apply_lemon_event(p_order_id uuid, p_event_key text, p_provider_order_id text,
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

revoke all on function public.reserve_lemon_checkout(uuid,text,uuid), public.bind_lemon_checkout(uuid,text),
    public.mark_lemon_checkout_uncertain(uuid), public.release_lemon_checkout(uuid),
    public.apply_lemon_event(uuid,text,text,text,text,integer,integer,integer,integer)
    from public, anon, authenticated;
grant execute on function public.reserve_lemon_checkout(uuid,text,uuid), public.bind_lemon_checkout(uuid,text),
    public.mark_lemon_checkout_uncertain(uuid), public.release_lemon_checkout(uuid),
    public.apply_lemon_event(uuid,text,text,text,text,integer,integer,integer,integer)
    to service_role;

commit;
