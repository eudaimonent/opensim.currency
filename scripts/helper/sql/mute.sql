-- --------------------------------------------------------
-- for Mute List
-- --------------------------------------------------------

CREATE TABLE `mute_list` (
    `agentID`       varchar(36)  NOT NULL,
    `muteID`        varchar(36)  NOT NULL,
    `muteName`      varchar(255) NOT NULL,
    `muteType`      int(10) unsigned NOT NULL default '0',
    `muteFlags`     int(10) unsigned NOT NULL default '0',
    `timestamp`     int(11) unsigned NOT NULL default '0',
    PRIMARY KEY  (`AgentID`,`MuteID`,`MuteName`)
) TYPE=MyISAM;


