begin;

-- User access reads already have SELECT grants and owner-only RLS policies.
-- Existing older installations without this optional function remain supported.
do $hardening$
begin
  if to_regprocedure('public.get_app_access()') is not null then
    alter function public.get_app_access() security invoker;
  end if;
end
$hardening$;

-- New objects must opt in to client access with explicit grants and RLS.
alter default privileges for role postgres revoke execute on functions from public, anon, authenticated;
alter default privileges for role postgres in schema public revoke all on tables from anon, authenticated;

commit;
