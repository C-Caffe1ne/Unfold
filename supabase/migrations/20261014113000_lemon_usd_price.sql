begin;

-- The Live Lemon store now sells the same variant at US$2.99 in USD.
-- Existing KRW orders keep their own currency, amount and variant for receipts and refunds.
do $$
begin
    if not exists (
        select 1
        from public.prices
        where id = 'unfold-global'
          and product_id = 'unfold'
          and market = 'GLOBAL'
          and currency = 'USD'
    ) then
        raise exception 'Unfold USD price contract is missing';
    end if;
end;
$$;

update public.prices set amount_minor = 299 where id = 'unfold-global';

-- Live variant IDs are operational data outside migrations. When the KRW price
-- has a Live mapping, the USD price takes over that sale with the same variant.
update public.prices as global
set live_enabled = kr.live_enabled
from public.prices as kr
where global.id = 'unfold-global'
  and kr.id = 'unfold-kr'
  and exists (select 1 from public.lemon_prices where price_id = 'unfold-kr' and environment = 'live');

update public.lemon_prices set price_id = 'unfold-global'
where price_id = 'unfold-kr' and environment = 'live';

update public.prices set live_enabled = false where id = 'unfold-kr';

commit;
