begin;

alter table public.orders drop constraint orders_provider_check;
alter table public.orders add constraint orders_provider_check check (provider in ('toss', 'lemon', 'paddle'));
alter table public.orders
    add column request_id uuid,
    add column provider_price_id text,
    add column provider_product_id text,
    add column checkout_state text check (checkout_state in ('retryable', 'creating', 'ready', 'uncertain')),
    add column tax_minor integer check (tax_minor >= 0),
    add column total_minor integer,
    add constraint order_total_matches check ((tax_minor is null and total_minor is null)
        or (tax_minor is not null and total_minor is not null and total_minor::bigint = amount_minor::bigint + tax_minor)),
    add constraint checkout_request_unique unique (user_id, provider, environment, request_id);

create table public.paddle_prices (
    price_id text not null references public.prices(id),
    environment text not null check (environment = 'test'),
    provider_price_id text not null check (provider_price_id ~ '^pri_[a-z0-9]{26}$'),
    provider_product_id text not null check (provider_product_id ~ '^pro_[a-z0-9]{26}$'),
    primary key (price_id, environment),
    unique (environment, provider_price_id)
);
insert into public.paddle_prices values
    ('unfold-kr', 'test', 'pri_01m3k742g5tva3gj528wa89jmf', 'pro_01m3k6t6spch04c1g8e6jk7dvn'),
    ('unfold-global', 'test', 'pri_01m3k6zzdpecd6ej4b99tem7qj', 'pro_01m3k6t6spch04c1g8e6jk7dvn');

create table public.paddle_events (
    environment text not null check (environment = 'test'),
    event_id text not null check (event_id ~ '^evt_[a-z0-9]{26}$'),
    order_id uuid not null references public.orders(id),
    transaction_id text not null,
    kind text not null check (kind in ('paid', 'refund')),
    adjustment_id text,
    subtotal_minor integer not null check (subtotal_minor >= 0),
    tax_minor integer not null check (tax_minor >= 0),
    total_minor integer not null check (total_minor > 0),
    full_refund boolean not null,
    processed_at timestamptz not null default now(),
    primary key (environment, event_id),
    check (total_minor::bigint = subtotal_minor::bigint + tax_minor)
);
create table public.paddle_refunds (
    environment text not null check (environment = 'test'),
    adjustment_id text not null check (adjustment_id ~ '^adj_[a-z0-9]{26}$'),
    order_id uuid not null references public.orders(id),
    subtotal_minor integer not null check (subtotal_minor >= 0),
    tax_minor integer not null check (tax_minor >= 0),
    total_minor integer not null check (total_minor > 0),
    primary key (environment, adjustment_id),
    check (total_minor::bigint = subtotal_minor::bigint + tax_minor)
);
alter table public.paddle_prices enable row level security;
alter table public.paddle_events enable row level security;
alter table public.paddle_refunds enable row level security;
revoke all on public.paddle_prices, public.paddle_events, public.paddle_refunds from public, anon, authenticated;
grant all on public.paddle_prices, public.paddle_events, public.paddle_refunds to service_role;

-- The request ID survives client retries. An uncertain external request is never sent twice.
create function public.reserve_paddle_checkout(p_user_id uuid, p_price_id text, p_request_id uuid)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    price public.prices%rowtype;
    mapping public.paddle_prices%rowtype;
begin
    if p_request_id is null then raise exception 'Invalid checkout request'; end if;
    perform pg_advisory_xact_lock(hashtextextended(p_user_id::text || '/unfold/test', 0));
    if exists (select 1 from public.entitlements where user_id = p_user_id and product_id = 'unfold'
        and environment = 'test' and status = 'active') then raise exception 'Already purchased'; end if;
    select * into target from public.orders where user_id = p_user_id and provider = 'paddle'
        and environment = 'test' and request_id = p_request_id;
    if found then
        if target.price_id <> p_price_id or target.status <> 'pending' then raise exception 'Checkout request conflict'; end if;
        if target.checkout_state = 'retryable' then
            update public.orders set checkout_state = 'creating', updated_at = now() where id = target.id returning * into target;
            return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
        end if;
        return jsonb_build_object('order', to_jsonb(target), 'acquired', false);
    end if;
    -- Reuse an unfinished same-price checkout even if a client loses its request ID.
    select * into target from public.orders where user_id = p_user_id and provider = 'paddle'
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
    if (select count(*) from public.orders where user_id = p_user_id and provider = 'paddle'
        and created_at > now() - interval '10 minutes') >= 5 then raise exception 'Checkout rate limited'; end if;
    select * into strict price from public.prices where id = p_price_id and product_id = 'unfold';
    select * into strict mapping from public.paddle_prices where price_id = p_price_id and environment = 'test';
    insert into public.orders(user_id, product_id, price_id, provider, environment, currency, amount_minor,
        request_id, provider_price_id, provider_product_id, checkout_state)
        values (p_user_id, price.product_id, price.id, 'paddle', 'test', price.currency, price.amount_minor,
            p_request_id, mapping.provider_price_id, mapping.provider_product_id, 'creating') returning * into target;
    return jsonb_build_object('order', to_jsonb(target), 'acquired', true);
end;
$$;

create function public.bind_paddle_checkout(p_order_id uuid, p_transaction_id text)
returns void language plpgsql security definer set search_path = '' as $$
declare target public.orders%rowtype;
begin
    if p_transaction_id is null or p_transaction_id !~ '^txn_[a-z0-9]{26}$' then raise exception 'Invalid transaction'; end if;
    select * into strict target from public.orders where id = p_order_id for update;
    if target.provider <> 'paddle' or target.environment <> 'test' or target.status <> 'pending'
        or (target.provider_order_id is not null and target.provider_order_id <> p_transaction_id)
        then raise exception 'Checkout binding conflict'; end if;
    update public.orders set provider_order_id = p_transaction_id, checkout_state = 'ready', updated_at = now() where id = p_order_id;
end;
$$;

create function public.mark_paddle_checkout_uncertain(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'uncertain', updated_at = now()
    where id = p_order_id and provider = 'paddle' and environment = 'test' and provider_order_id is null;
$$;

-- Used only if the read-only price preflight failed, before POST /transactions was attempted.
create function public.release_paddle_checkout(p_order_id uuid)
returns void language sql security definer set search_path = '' as $$
    update public.orders set checkout_state = 'retryable', updated_at = now()
    where id = p_order_id and provider = 'paddle' and environment = 'test'
        and checkout_state = 'creating' and provider_order_id is null;
$$;

-- Only normalized, signature-verified facts reach this atomic receipt/refund/entitlement transaction.
create function public.apply_paddle_event(p_order_id uuid, p_event_id text, p_transaction_id text,
    p_kind text, p_currency text, p_subtotal integer, p_tax integer, p_total integer,
    p_adjustment_id text default null, p_full_refund boolean default false)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    receipt public.paddle_events%rowtype;
    adjustment public.paddle_refunds%rowtype;
    refund_subtotal bigint;
    refund_tax bigint;
    refund_total bigint;
begin
    select * into strict target from public.orders where id = p_order_id;
    perform pg_advisory_xact_lock(hashtextextended(target.user_id::text || '/' || target.product_id || '/' || target.environment, 0));
    select * into strict target from public.orders where id = p_order_id for update;
    if target.provider <> 'paddle' or target.environment <> 'test' or target.provider_order_id is null
        or p_transaction_id is distinct from target.provider_order_id or p_currency is distinct from target.currency
        or p_kind is null or p_kind not in ('paid', 'refund') or p_full_refund is null
        or p_subtotal is null or p_subtotal < 0 or p_tax is null or p_tax < 0 or p_total is null or p_total <= 0
        or p_total::bigint <> p_subtotal::bigint + p_tax
        or p_subtotal > target.amount_minor
        or (p_kind = 'paid' and (p_subtotal <> target.amount_minor or p_adjustment_id is not null or p_full_refund))
        or (p_kind = 'refund' and (p_adjustment_id is null or p_adjustment_id !~ '^adj_[a-z0-9]{26}$'))
        or (p_full_refund and p_subtotal <> target.amount_minor)
        then raise exception 'Paddle event does not match order'; end if;
    select * into receipt from public.paddle_events where environment = 'test' and event_id = p_event_id;
    if found then
        if (receipt.order_id,receipt.transaction_id,receipt.kind,receipt.adjustment_id,receipt.subtotal_minor,receipt.tax_minor,receipt.total_minor,receipt.full_refund)
            is distinct from (p_order_id,p_transaction_id,p_kind,p_adjustment_id,p_subtotal,p_tax,p_total,p_full_refund)
            then raise exception 'Paddle event conflict'; end if;
        return false;
    end if;
    if p_kind = 'paid' or p_full_refund then
        if target.total_minor is not null and (target.total_minor <> p_total or target.tax_minor <> p_tax)
            then raise exception 'Paddle totals conflict'; end if;
        update public.orders set tax_minor = p_tax, total_minor = p_total where id = p_order_id returning * into target;
    end if;
    if p_kind = 'refund' then
        select * into adjustment from public.paddle_refunds where environment = 'test' and adjustment_id = p_adjustment_id;
        if found then
            if (adjustment.order_id, adjustment.subtotal_minor, adjustment.tax_minor, adjustment.total_minor)
                is distinct from (p_order_id, p_subtotal, p_tax, p_total) then raise exception 'Paddle adjustment conflict'; end if;
        else
            insert into public.paddle_refunds values ('test', p_adjustment_id, p_order_id, p_subtotal, p_tax, p_total);
        end if;
    end if;
    select coalesce(sum(subtotal_minor),0), coalesce(sum(tax_minor),0), coalesce(sum(total_minor),0)
        into refund_subtotal, refund_tax, refund_total from public.paddle_refunds where order_id = p_order_id;
    if refund_subtotal > target.amount_minor or (target.total_minor is not null
        and (refund_total > target.total_minor or refund_tax > target.tax_minor)) then raise exception 'Paddle refund exceeds payment'; end if;
    insert into public.paddle_events values ('test', p_event_id, p_order_id, p_transaction_id, p_kind,
        p_adjustment_id, p_subtotal, p_tax, p_total, p_full_refund, now());
    if target.total_minor is not null and refund_total = target.total_minor and refund_subtotal = target.amount_minor then
        perform public.apply_verified_payment(p_order_id,'paddle','test',p_event_id,p_transaction_id,'refunded',p_currency,target.amount_minor);
    elsif p_kind = 'paid' then
        perform public.apply_verified_payment(p_order_id,'paddle','test',p_event_id,p_transaction_id,'paid',p_currency,target.amount_minor);
    end if;
    return true;
end;
$$;

revoke all on function public.reserve_paddle_checkout(uuid,text,uuid), public.bind_paddle_checkout(uuid,text),
    public.mark_paddle_checkout_uncertain(uuid), public.release_paddle_checkout(uuid), public.apply_paddle_event(uuid,text,text,text,text,integer,integer,integer,text,boolean)
    from public, anon, authenticated;
grant execute on function public.reserve_paddle_checkout(uuid,text,uuid), public.bind_paddle_checkout(uuid,text),
    public.mark_paddle_checkout_uncertain(uuid), public.release_paddle_checkout(uuid), public.apply_paddle_event(uuid,text,text,text,text,integer,integer,integer,text,boolean)
    to service_role;
commit;
