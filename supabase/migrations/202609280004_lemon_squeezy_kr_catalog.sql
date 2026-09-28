begin;

do $$
begin
    if not exists (
        select 1
        from public.prices
        where id = 'unfold-kr'
          and product_id = 'unfold'
          and market = 'KR'
          and currency = 'KRW'
          and amount_minor = 4900
    ) then
        raise exception 'Unfold KRW price contract is missing';
    end if;
end;
$$;

insert into public.lemon_prices(price_id, environment, store_id, variant_id, product_id, checkout_host)
values ('unfold-kr', 'test', '485125', '2176689', '1393777', 'dokhustudio.lemonsqueezy.com')
on conflict (price_id, environment) do update
set store_id = excluded.store_id,
    variant_id = excluded.variant_id,
    product_id = excluded.product_id,
    checkout_host = excluded.checkout_host;

-- The global catalog will be added through a later reviewed migration.
delete from public.lemon_prices
where price_id = 'unfold-global'
  and environment = 'test';

commit;
