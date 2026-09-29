begin;

-- Personal and company administrator accounts confirmed on 2026-09-29.
-- Stable Auth user IDs keep later email or provider metadata changes from altering access.
insert into public.account_roles(user_id, role)
select id, 'admin' from auth.users
where id in (
    'a295b416-c0d9-41b9-b48c-5479fbad78cd'::uuid,
    '07ea6ef4-2ac2-448f-a5a0-62c2697cc9a3'::uuid
)
on conflict (user_id) do update set role = excluded.role;

commit;
