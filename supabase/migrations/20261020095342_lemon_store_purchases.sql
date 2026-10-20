-- A purchase from the public Lemon store page carries no Unfold order. Record it, and link it to the
-- Google account with the same verified email as an ordinary paid order, so refunds and repeat
-- purchases follow apply_verified_payment. Linking happens on delivery when that account exists,
-- otherwise at its Google sign-in or app access check.
create table public.lemon_store_purchases (
    environment text not null check (environment in ('test', 'live')),
    provider_order_id text not null check (provider_order_id ~ '^[1-9][0-9]{0,18}$'),
    email text not null check (email = lower(btrim(email)) and length(email) <= 320
        and email ~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'),
    price_id text not null references public.prices(id),
    provider_store_id text not null,
    provider_product_id text not null,
    provider_price_id text not null,
    currency text not null check (currency in ('KRW', 'USD')),
    amount_minor integer not null check (amount_minor > 0),
    subtotal_minor numeric not null check (subtotal_minor > 0),
    tax_minor numeric not null check (tax_minor >= 0),
    total_minor numeric not null check (total_minor = subtotal_minor + tax_minor),
    refunded_total_minor numeric not null default 0
        check (refunded_total_minor >= 0 and refunded_total_minor <= total_minor),
    order_id uuid unique references public.orders(id),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (environment, provider_order_id)
);
create index lemon_store_purchases_unlinked on public.lemon_store_purchases (email) where order_id is null;

create table public.lemon_store_events (
    environment text not null,
    event_key text not null check (length(event_key) between 1 and 256),
    provider_order_id text not null,
    kind text not null check (kind in ('paid', 'refund')),
    refunded_total_minor numeric not null,
    processed_at timestamptz not null default now(),
    primary key (environment, event_key),
    foreign key (environment, provider_order_id) references public.lemon_store_purchases(environment, provider_order_id)
);

alter table public.lemon_store_purchases enable row level security;
alter table public.lemon_store_events enable row level security;
revoke all on public.lemon_store_purchases, public.lemon_store_events from public, anon, authenticated;

create function public.lemon_store_price(p_environment text, p_store_id text, p_product_id text, p_variant_id text)
returns table(price_id text, currency text, amount_minor integer)
language sql stable security definer set search_path = '' as $$
    select p.id, p.currency, p.amount_minor
      from public.lemon_prices lp join public.prices p on p.id = lp.price_id
     where lp.environment = p_environment and lp.store_id = p_store_id
       and lp.product_id = p_product_id and lp.variant_id = p_variant_id and p.product_id = 'unfold'
$$;

-- The one account whose Google identity verified this email; none when it is missing or shared.
create function public.lemon_store_purchase_owner(p_email text) returns uuid
language plpgsql stable security definer set search_path = '' as $$
declare owners uuid[];
begin
    select array_agg(distinct i.user_id) into owners from auth.identities i
     where i.provider = 'google' and i.email = p_email
       and i.identity_data->>'email_verified' = 'true';
    return case when cardinality(owners) = 1 then owners[1] end;
end $$;

create function public.link_lemon_store_purchase(p_environment text, p_provider_order_id text, p_user uuid)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
    purchase public.lemon_store_purchases%rowtype;
    linked uuid;
begin
    -- The purchase row lock alone serializes linking; apply_verified_payment takes the account lock.
    select * into strict purchase from public.lemon_store_purchases
     where environment = p_environment and provider_order_id = p_provider_order_id for update;
    if purchase.order_id is not null or purchase.refunded_total_minor = purchase.total_minor then return false; end if;
    insert into public.orders(user_id, product_id, price_id, provider, environment, currency, amount_minor,
        provider_store_id, provider_product_id, provider_price_id, tax_minor, total_minor, refunded_total_minor)
        values (p_user, 'unfold', purchase.price_id, 'lemon', p_environment, purchase.currency, purchase.amount_minor,
            purchase.provider_store_id, purchase.provider_product_id, purchase.provider_price_id,
            purchase.tax_minor, purchase.total_minor, purchase.refunded_total_minor)
        returning id into linked;
    update public.lemon_store_purchases set order_id = linked, updated_at = now()
     where environment = p_environment and provider_order_id = p_provider_order_id;
    perform public.apply_verified_payment(linked, 'lemon', p_environment, 'store:' || p_provider_order_id,
        p_provider_order_id, 'paid', purchase.currency, purchase.amount_minor);
    return true;
end $$;

create function public.record_lemon_store_purchase(p_environment text, p_event_key text, p_provider_order_id text,
    p_kind text, p_email text, p_price_id text, p_currency text, p_item_price numeric, p_subtotal numeric,
    p_tax numeric, p_total numeric, p_refunded_total numeric)
returns boolean language plpgsql security definer set search_path = '' as $$
declare
    v_email text := lower(btrim(p_email));
    price public.prices%rowtype;
    mapping public.lemon_prices%rowtype;
    purchase public.lemon_store_purchases%rowtype;
    receipt public.lemon_store_events%rowtype;
    owner uuid;
begin
    select * into price from public.prices where id = p_price_id;
    select * into mapping from public.lemon_prices where price_id = p_price_id and environment = p_environment;
    if p_environment is null or p_environment not in ('test', 'live') or price.id is null or mapping.price_id is null
        or p_event_key is null or length(p_event_key) not between 1 and 256
        or p_provider_order_id is null or p_provider_order_id !~ '^[1-9][0-9]{0,18}$'
        or p_kind is null or p_kind not in ('paid', 'refund')
        or v_email is null or length(v_email) > 320 or v_email !~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'
        or p_currency is distinct from price.currency or p_item_price is distinct from price.amount_minor
        or p_subtotal is null or p_subtotal <= 0 or p_tax is null or p_tax < 0
        or p_total is null or p_total <> p_subtotal + p_tax
        or p_refunded_total is null or p_refunded_total < 0 or p_refunded_total > p_total
        or (p_kind = 'paid' and p_refunded_total <> 0)
        then raise exception 'Lemon store purchase does not match catalog'; end if;
    select * into receipt from public.lemon_store_events where environment = p_environment and event_key = p_event_key;
    if found then
        if (receipt.provider_order_id, receipt.kind, receipt.refunded_total_minor)
            is distinct from (p_provider_order_id, p_kind, p_refunded_total)
            then raise exception 'Lemon store event conflict'; end if;
        return false;
    end if;
    select * into purchase from public.lemon_store_purchases
     where environment = p_environment and provider_order_id = p_provider_order_id for update;
    if found then
        if (purchase.email, purchase.price_id, purchase.currency, purchase.amount_minor,
            purchase.subtotal_minor, purchase.tax_minor, purchase.total_minor)
            is distinct from (v_email, p_price_id, p_currency, p_item_price::integer, p_subtotal, p_tax, p_total)
            then raise exception 'Lemon store purchase conflict'; end if;
        update public.lemon_store_purchases
           set refunded_total_minor = greatest(refunded_total_minor, p_refunded_total), updated_at = now()
         where environment = p_environment and provider_order_id = p_provider_order_id
        returning * into purchase;
    else
        insert into public.lemon_store_purchases(environment, provider_order_id, email, price_id, provider_store_id,
            provider_product_id, provider_price_id, currency, amount_minor, subtotal_minor, tax_minor, total_minor,
            refunded_total_minor)
            values (p_environment, p_provider_order_id, v_email, p_price_id, mapping.store_id, mapping.product_id,
                mapping.variant_id, p_currency, p_item_price::integer, p_subtotal, p_tax, p_total, p_refunded_total)
            returning * into purchase;
    end if;
    insert into public.lemon_store_events(environment, event_key, provider_order_id, kind, refunded_total_minor)
        values (p_environment, p_event_key, p_provider_order_id, p_kind, p_refunded_total);
    if purchase.order_id is not null then
        update public.orders set refunded_total_minor = purchase.refunded_total_minor, updated_at = now()
         where id = purchase.order_id;
        if purchase.refunded_total_minor = purchase.total_minor then
            perform public.apply_verified_payment(purchase.order_id, 'lemon', p_environment, p_event_key,
                p_provider_order_id, 'refunded', purchase.currency, purchase.amount_minor);
        end if;
    elsif purchase.refunded_total_minor < purchase.total_minor then
        owner := public.lemon_store_purchase_owner(purchase.email);
        if owner is not null then perform public.link_lemon_store_purchase(p_environment, p_provider_order_id, owner); end if;
    end if;
    return true;
end $$;

-- Links unlinked store purchases bought with this account's verified Google email.
create function public.claim_lemon_store_purchases_for(p_user uuid) returns integer
language plpgsql security definer set search_path = '' as $$
declare
    linked integer := 0;
    purchase record;
begin
    for purchase in
        select sp.environment, sp.provider_order_id from public.lemon_store_purchases sp
         where sp.order_id is null and sp.refunded_total_minor < sp.total_minor
           and sp.email in (select i.email from auth.identities i where i.user_id = p_user and i.provider = 'google')
           and public.lemon_store_purchase_owner(sp.email) = p_user
    loop
        if public.link_lemon_store_purchase(purchase.environment, purchase.provider_order_id, p_user) then
            linked := linked + 1;
        end if;
    end loop;
    return linked;
end $$;

create function public.claim_lemon_store_purchases() returns integer
language sql security definer set search_path = '' as $$
    select case when auth.uid() is null then 0 else public.claim_lemon_store_purchases_for(auth.uid()) end
$$;

-- Google sign-in on any client (web or app) links purchases bought before the account existed.
-- A failure never blocks sign-in; the app's access check claims again.
create function public.link_lemon_store_purchases_on_sign_in() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    begin
        perform public.claim_lemon_store_purchases_for(new.user_id);
    exception when others then
        raise warning 'lemon store purchase link skipped: %', sqlstate;
    end;
    return null;
end $$;
create trigger link_lemon_store_purchases after insert or update on auth.identities
    for each row when (new.provider = 'google') execute function public.link_lemon_store_purchases_on_sign_in();

-- get_app_access lives outside these migrations (docs/access-codes.md). Where it exists, keep its
-- access rule and let the account claim store purchases bought with its email first.
do $access$
begin
    if to_regprocedure('public.get_app_access()') is null then return; end if;
    create or replace function public.get_app_access() returns text
    language plpgsql volatile set search_path = '' as $fn$
    declare v_user uuid := auth.uid();
    begin
      if v_user is null then raise exception 'Authentication required'; end if;
      begin
        perform public.claim_lemon_store_purchases();
      exception when others then
        raise warning 'lemon store purchase claim skipped: %', sqlstate;
      end;
      if exists (select 1 from public.entitlements where user_id = v_user and product_id = 'unfold'
          and environment = 'live' and status = 'active')
        or exists (select 1 from public.free_access_grants where user_id = v_user and product_id = 'unfold'
          and status = 'active' and (expires_at is null or expires_at > now())) then return 'active'; end if;
      if exists (select 1 from public.entitlements where user_id = v_user and product_id = 'unfold'
          and environment = 'live' and status = 'revoked')
        or exists (select 1 from public.free_access_grants where user_id = v_user and product_id = 'unfold')
        then return 'revoked'; end if;
      return 'unowned';
    end $fn$;
    revoke all on function public.get_app_access() from public, anon;
    grant execute on function public.get_app_access() to authenticated;
end
$access$;

revoke all on function public.lemon_store_price(text, text, text, text),
    public.lemon_store_purchase_owner(text),
    public.link_lemon_store_purchase(text, text, uuid),
    public.record_lemon_store_purchase(text, text, text, text, text, text, text, numeric, numeric, numeric, numeric, numeric),
    public.claim_lemon_store_purchases_for(uuid),
    public.claim_lemon_store_purchases(),
    public.link_lemon_store_purchases_on_sign_in()
    from public, anon, authenticated;
grant execute on function public.lemon_store_price(text, text, text, text),
    public.record_lemon_store_purchase(text, text, text, text, text, text, text, numeric, numeric, numeric, numeric, numeric)
    to service_role;
grant execute on function public.claim_lemon_store_purchases() to authenticated;
