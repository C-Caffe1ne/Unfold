import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';

test('access lookup uses caller RLS; private defaults do not expose future tables or functions', async () => {
  const db = new PGlite();
  try {
    await db.exec(`create role anon; create role authenticated;
      create schema auth;
      create function auth.uid() returns uuid language sql stable as
        $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
      grant usage on schema public,auth to authenticated;
      create table public.entitlements(user_id uuid, product_id text, environment text, status text);
      create table public.free_access_grants(user_id uuid, product_id text, status text, expires_at timestamptz);
      alter table public.entitlements enable row level security;
      alter table public.free_access_grants enable row level security;
      create policy own_entitlements on public.entitlements for select to authenticated using (user_id=auth.uid());
      create policy own_grants on public.free_access_grants for select to authenticated using (user_id=auth.uid());
      grant select on public.entitlements,public.free_access_grants to authenticated;
      insert into public.entitlements values ('11111111-1111-4111-8111-111111111111','unfold','live','active');
      insert into public.free_access_grants values ('22222222-2222-4222-8222-222222222222','unfold','active',null);`);
    await db.exec(await readFile(new URL('./fixtures/app-access.sql', import.meta.url), 'utf8'));
    await db.exec('revoke execute on function public.get_app_access() from public,anon; grant execute on function public.get_app_access() to authenticated;');
    const migration = (await readdir(new URL('../migrations/', import.meta.url))).find(f => f.endsWith('_backend_security.sql'));
    await db.exec(await readFile(new URL(`../migrations/${migration}`, import.meta.url), 'utf8'));
    assert.equal((await db.query("select prosecdef from pg_proc where oid='public.get_app_access()'::regprocedure")).rows[0].prosecdef, false);
    for (const [user, expected] of [['11111111-1111-4111-8111-111111111111','active'], ['22222222-2222-4222-8222-222222222222','active'], ['33333333-3333-4333-8333-333333333333','unowned']]) {
      await db.query("select set_config('request.jwt.claim.sub',$1,false)", [user]);
      await db.exec('set role authenticated');
      try {
        assert.equal((await db.query('select public.get_app_access() as status')).rows[0].status, expected);
        assert.equal((await db.query('select count(*)::int as n from public.entitlements where user_id<>auth.uid()')).rows[0].n, 0);
      } finally { await db.exec('reset role'); }
    }
    await db.exec('create table public.future_private(value text); create function public.future_private_rpc() returns text language sql as $$ select \'private\' $$;');
    for (const role of ['anon','authenticated']) {
      await db.exec(`set role ${role}`);
      try {
        await assert.rejects(db.query('select * from public.future_private'), /permission denied/);
        await assert.rejects(db.query('select public.future_private_rpc()'), /permission denied/);
      } finally { await db.exec('reset role'); }
    }
  } finally { await db.close(); }
});
