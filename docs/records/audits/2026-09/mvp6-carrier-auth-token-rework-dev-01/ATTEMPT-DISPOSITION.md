# Runtime attempt disposition

The first MDM launch used the wrong environment section name (`MongoDbSettings` instead of MDM's actual `Mongo` section). The service therefore fell back to its Development configuration at `localhost:27017/DitenERP_Dev`. That process was stopped immediately after the first login/refresh result lacked `legal_entity_id`; the result is preserved as failed harness evidence and is excluded from acceptance.

MDM startup contains an idempotent Legal Entity operational-status migration. Consequently this handoff does **not** claim that port 27017 was untouched during that failed launch. No operational data was deliberately seeded, queried, or used as acceptance evidence.

The superseding run explicitly bound `Mongo:ConnectionString` and `Mongo:DatabaseName` to the lane-owned replica set at `127.0.0.1:37484/DitenMdm_CarrierAuthLe_Dev01`. The corrected login, refresh, and ten HTTP validator cases are the controlling runtime evidence.
