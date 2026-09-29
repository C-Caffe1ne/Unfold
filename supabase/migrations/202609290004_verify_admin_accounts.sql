begin;

do $$
declare
    expected uuid[] := array[
        'a295b416-c0d9-41b9-b48c-5479fbad78cd'::uuid,
        '07ea6ef4-2ac2-448f-a5a0-62c2697cc9a3'::uuid
    ];
begin
    -- Fresh local test databases do not contain production Auth identities.
    -- When either confirmed identity exists, require both assignments atomically.
    if exists (select 1 from auth.users where id = any(expected))
        and (select count(*) from public.account_roles where user_id = any(expected) and role = 'admin') <> 2 then
        raise exception 'Confirmed administrator accounts were not both assigned';
    end if;
end;
$$;

commit;
