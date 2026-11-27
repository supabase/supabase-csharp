create function public.echo_client_info()
    returns text
    language sql
    stable
as
$$
select current_setting('request.headers', true)::json ->> 'x-client-info';
$$;

grant execute on function public.echo_client_info() to anon, authenticated, service_role;
