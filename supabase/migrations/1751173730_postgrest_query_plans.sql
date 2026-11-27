alter role authenticator set pgrst.db_plan_enabled to 'true';

notify pgrst, 'reload config';
