# Discarded setup attempt 01

The first fresh HTTP run reached the transaction rollback section, then stopped before an application assertion because the new isolated mongod omitted `--setParameter enableTestCommands=1`. Mongo rejected `configureFailPoint`. No count from this attempt is used. The services were stopped cleanly, the launch helper was corrected, and the entire suite was rerun from `seed()` which clears every collection.
