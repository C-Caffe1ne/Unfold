begin;

create table public.account_roles (
    user_id uuid primary key references auth.users(id) on delete cascade,
    role text not null check (role = 'admin'),
    created_at timestamptz not null default now()
);

alter table public.account_roles enable row level security;
revoke all on public.account_roles from public, anon, authenticated;
grant select on public.account_roles to authenticated;
grant all on public.account_roles to service_role;
create policy own_account_role on public.account_roles for select to authenticated
    using ((select auth.uid()) = user_id);

commit;
