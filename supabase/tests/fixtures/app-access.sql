create function public.get_app_access() returns text
language plpgsql stable security definer set search_path = '' as $$
declare v_user uuid := auth.uid();
begin
  if v_user is null then raise exception 'Authentication required'; end if;
  if exists (select 1 from public.entitlements where user_id = v_user and product_id = 'unfold'
      and environment = 'live' and status = 'active')
    or exists (select 1 from public.free_access_grants where user_id = v_user and product_id = 'unfold'
      and status = 'active' and (expires_at is null or expires_at > now())) then return 'active'; end if;
  if exists (select 1 from public.entitlements where user_id = v_user and product_id = 'unfold'
      and environment = 'live' and status = 'revoked')
    or exists (select 1 from public.free_access_grants where user_id = v_user and product_id = 'unfold')
    then return 'revoked'; end if;
  return 'unowned';
end $$;
