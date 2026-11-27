alter role authenticator set pgrst.db_tx_end to 'commit-allow-override';

notify pgrst, 'reload config';
