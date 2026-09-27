begin;

create table public.products (
    id text primary key,
    name text not null
);
create table public.prices (
    id text primary key,
    product_id text not null references public.products(id),
    market text not null check (market in ('KR', 'GLOBAL')),
    currency text not null check (currency in ('KRW', 'USD')),
    amount_minor integer not null check (amount_minor > 0),
    live_enabled boolean not null default false,
    unique (product_id, market),
    check ((market = 'KR' and currency = 'KRW') or (market = 'GLOBAL' and currency = 'USD'))
);
insert into public.products values ('unfold', 'Unfold');
insert into public.prices (id, product_id, market, currency, amount_minor) values
    ('unfold-kr', 'unfold', 'KR', 'KRW', 4900),
    ('unfold-global', 'unfold', 'GLOBAL', 'USD', 399);

create table public.orders (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id),
    product_id text not null references public.products(id),
    price_id text not null references public.prices(id),
    provider text not null check (provider in ('toss', 'lemon')),
    environment text not null check (environment in ('test', 'live')),
    provider_order_id text,
    currency text not null check (currency in ('KRW', 'USD')),
    amount_minor integer not null check (amount_minor > 0),
    status text not null default 'pending' check (status in ('pending', 'paid', 'refunded')),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (provider, environment, provider_order_id)
);
create index orders_owner_product on public.orders(user_id, product_id, environment);

create table public.entitlements (
    user_id uuid not null references auth.users(id),
    product_id text not null references public.products(id),
    environment text not null check (environment in ('test', 'live')),
    status text not null check (status in ('active', 'revoked')),
    updated_at timestamptz not null default now(),
    primary key (user_id, product_id, environment)
);

-- A receipt of a verified server event, not an inbox of untrusted webhook payloads.
create table public.payment_events (
    provider text not null,
    environment text not null,
    event_key text not null check (length(event_key) between 1 and 256),
    order_id uuid not null references public.orders(id),
    provider_order_id text not null check (length(provider_order_id) between 1 and 256),
    event_kind text not null check (event_kind in ('paid', 'refunded')),
    currency text not null,
    amount_minor integer not null,
    processed_at timestamptz not null default now(),
    primary key (provider, environment, event_key)
);

alter table public.products enable row level security;
alter table public.prices enable row level security;
alter table public.orders enable row level security;
alter table public.entitlements enable row level security;
alter table public.payment_events enable row level security;

revoke all on public.products, public.prices, public.orders, public.entitlements, public.payment_events
    from public, anon, authenticated;
grant select on public.products, public.prices to anon, authenticated;
grant select on public.orders, public.entitlements to authenticated;
grant all on public.products, public.prices, public.orders, public.entitlements, public.payment_events to service_role;
create policy product_catalog on public.products for select to anon, authenticated using (true);
create policy price_catalog on public.prices for select to anon, authenticated using (true);
create policy own_orders on public.orders for select to authenticated using ((select auth.uid()) = user_id);
create policy own_entitlements on public.entitlements for select to authenticated using ((select auth.uid()) = user_id);

-- Only the server creates orders. The caller cannot supply a price or currency.
create function public.create_pending_order(p_user_id uuid, p_price_id text, p_provider text, p_environment text)
returns uuid language plpgsql security definer set search_path = '' as $$
declare
    price public.prices%rowtype;
    order_id uuid;
begin
    select * into strict price from public.prices where id = p_price_id;
    if p_environment = 'live' and not price.live_enabled then
        raise exception 'Live checkout is not configured';
    end if;
    insert into public.orders(user_id, product_id, price_id, provider, environment, currency, amount_minor)
        values (p_user_id, price.product_id, price.id, p_provider, p_environment, price.currency, price.amount_minor)
        returning id into order_id;
    return order_id;
end;
$$;

-- Adapters must verify provider signature AND order/product/amount/mode before calling.
-- Full refunds only; partial refund policy is deliberately not inferred here.
create function public.apply_verified_payment(
    p_order_id uuid, p_provider text, p_environment text, p_event_key text,
    p_provider_order_id text, p_kind text, p_currency text, p_amount_minor integer
)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
    target public.orders%rowtype;
    receipt public.payment_events%rowtype;
    inserted integer;
begin
    select * into strict target from public.orders where id = p_order_id;
    -- Serialize all purchases for this account/product, including concurrent refunds.
    perform pg_advisory_xact_lock(hashtextextended(target.user_id::text || '/' || target.product_id || '/' || target.environment, 0));
    select * into strict target from public.orders where id = p_order_id for update;
    if p_kind is null or p_kind not in ('paid', 'refunded')
        or p_provider is distinct from target.provider
        or p_environment is distinct from target.environment
        or p_currency is distinct from target.currency
        or p_amount_minor is distinct from target.amount_minor
        or (target.provider_order_id is not null and target.provider_order_id is distinct from p_provider_order_id) then
        raise exception 'Payment does not match order';
    end if;
    insert into public.payment_events(provider, environment, event_key, order_id, provider_order_id, event_kind, currency, amount_minor)
        values (p_provider, p_environment, p_event_key, p_order_id, p_provider_order_id, p_kind, p_currency, p_amount_minor)
        on conflict do nothing;
    get diagnostics inserted = row_count;
    if inserted = 0 then
        select * into strict receipt from public.payment_events
            where provider = p_provider and environment = p_environment and event_key = p_event_key;
        if (receipt.order_id, receipt.provider_order_id, receipt.event_kind, receipt.currency, receipt.amount_minor)
            is distinct from (p_order_id, p_provider_order_id, p_kind, p_currency, p_amount_minor) then
            raise exception 'Event key already used for a different payment';
        end if;
        return false;
    end if;

    update public.orders set
        status = case when status = 'refunded' or p_kind = 'refunded' then 'refunded' else 'paid' end,
        provider_order_id = p_provider_order_id, updated_at = now()
        where id = p_order_id;
    insert into public.entitlements(user_id, product_id, environment, status)
        values (target.user_id, target.product_id, target.environment,
            case when exists (select 1 from public.orders where user_id = target.user_id
                and product_id = target.product_id and environment = target.environment and status = 'paid')
                then 'active' else 'revoked' end)
        on conflict (user_id, product_id, environment)
        do update set status = excluded.status, updated_at = now();
    return true;
end;
$$;

revoke all on function public.create_pending_order(uuid, text, text, text) from public, anon, authenticated;
revoke all on function public.apply_verified_payment(uuid, text, text, text, text, text, text, integer) from public, anon, authenticated;
grant execute on function public.create_pending_order(uuid, text, text, text) to service_role;
grant execute on function public.apply_verified_payment(uuid, text, text, text, text, text, text, integer) to service_role;
commit;
