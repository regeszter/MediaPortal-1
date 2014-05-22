USE %TvLibrary%;

ALTER TABLE mptvdb.channelgroup ADD COLUMN pinCode VARCHAR(5) NULL  AFTER sortOrder ;

UPDATE Version SET versionNumber=62;
