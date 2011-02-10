<?php
//
// Configration file
//
//
//

if (!defined('ENV_HELPER_URL'))  define('ENV_HELPER_URL',  'http://www.nsl.tuis.ac.jp/xoops/modules/xoopensim/helper');
if (!defined('ENV_HELPER_PATH')) define('ENV_HELPER_PATH', '/home/apache/htdocs/xoops/modules/xoopensim/helper');


$GLOBALS['xmlrpc_internalencoding'] = 'UTF-8';


//////////////////////////////////////////////////////////////////////////////////i
//
// Valiables for OpenSim
//

define('OPENSIM_DB_HOST',			'localhost');
define('OPENSIM_DB_NAME',			'opensim');
define('OPENSIM_DB_USER',			'opensim_user');
define('OPENSIM_DB_PASS',			'opensim_pass');

define('USE_CURRENCY_SERVER',		1);
define('CURRENCY_SCRIPT_KEY',		'123456789');
define('USER_SERVER_URI',			'http://202.26.159.200:8002');

define('XMLGROUP_RKEY',				'1234');
define('XMLGROUP_WKEY',				'7890');


//
define('SYSURL',					ENV_HELPER_URL);
define('USE_UTC_TIME',				1);

if (USE_UTC_TIME) date_default_timezone_set('UTC');




//////////////////////////////////////////////////////////////////////////////////
//
// External NSL Modules
//

// Currency
define('CURRENCY_DB_HOST',			OPENSIM_DB_HOST);
define('CURRENCY_DB_NAME',			OPENSIM_DB_NAME);
define('CURRENCY_DB_USER',			OPENSIM_DB_USER);
define('CURRENCY_DB_PASS',			OPENSIM_DB_PASS);
define('CURRENCY_MONEY_TBL',	  	'balances');
define('CURRENCY_TRANSACTION_TBL',	'transactions');


// Offline Message
define('OFFLINE_DB_HOST',			OPENSIM_DB_HOST);
define('OFFLINE_DB_NAME',	 		OPENSIM_DB_NAME);
define('OFFLINE_DB_USER',			OPENSIM_DB_USER);
define('OFFLINE_DB_PASS',			OPENSIM_DB_PASS);
define('OFFLINE_MESSAGE_TBL',	   	'offline_message');


// MuteList 
define('MUTE_DB_HOST',				OFFLINE_DB_HOST);
define('MUTE_DB_NAME',				OFFLINE_DB_NAME);
define('MUTE_DB_USER',				OFFLINE_DB_USER);
define('MUTE_DB_PASS',				OFFLINE_DB_PASS);
define('MUTE_LIST_TBL',				'mute_list');



//////////////////////////////////////////////////////////////////////////////////
//
// External other Modules
//

// XML Group.  see also xmlgroups_config.php 
define('XMLGROUP_ACTIVE_TBL',		'group_active');
define('XMLGROUP_LIST_TBL',			'group_list');
define('XMLGROUP_INVITE_TBL',		'group_invite');
define('XMLGROUP_MEMBERSHIP_TBL',   'group_membership');
define('XMLGROUP_NOTICE_TBL',		'group_notice');
define('XMLGROUP_ROLE_MEMBER_TBL',	'group_rolemembership');
define('XMLGROUP_ROLE_TBL',			'group_role');


// Avatar Profile. see also profile_config.php
define('PROFILE_CLASSIFIEDS_TBL',   'prof_classifieds');
define('PROFILE_USERNOTES_TBL',	 	'prof_usernotes');
define('PROFILE_USERPICKS_TBL',	 	'prof_userpicks');
define('PROFILE_USERPROFILE_TBL',   'prof_userprofile');
define('PROFILE_USERSETTINGS_TBL',  'prof_usersettings');


// Search the In World. see also search_config.php 
define('SEARCH_ALLPARCELS_TBL',	 	'search_allparcels');
define('SEARCH_EVENTS_TBL',		 	'search_events');
define('SEARCH_HOSTSREGISTER_TBL',  'search_hostsregister');
define('SEARCH_OBJECTS_TBL',		'search_objects');
define('SEARCH_PARCELS_TBL',		'search_parcels');
define('SEARCH_PARCELSALES_TBL',	'search_parcelsales');
define('SEARCH_POPULARPLACES_TBL',  'search_popularplaces');
define('SEARCH_REGIONS_TBL',		'search_regions');
define('SEARCH_CLASSIFIEDS_TBL',	PROFILE_CLASSIFIEDS_TBL);




//
if (!defined('ENV_READED_CONFIG')) define('ENV_READED_CONFIG', 'YES');

?>
