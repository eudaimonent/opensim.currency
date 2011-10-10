<?php

/*

This file acts as a client to the xmlrpc.php service, to test a call to the getAgentGroupMemberships() method.  This can be used to debug whether or not your installation is failing server side due to php, apache, or other similiar errors.

*/
    include("phpxmlrpclib/xmlrpc.inc");
    include("phpxmlrpclib/xmlrpcs.inc");

	//$client = new xmlrpc_client('http://www.opensim.tuis.ac.jp/modules/xoopensim/helper/xmlgroups.php');
	$client->return_type = 'phpvals';
	$client->SetDebug(3);
			
	$verifyParams = new xmlrpcval(array('RequestingAgentID' => new xmlrpcval('00000000-0000-0000-0000-000000000000', 'string')
									   ,'RequestingSessionID'  => new xmlrpcval('00000000-0000-0000-0000-000000000000', 'string')
									   ,'RequestingAgentUserService'  => new xmlrpcval('http://opensim.tuis.ac.jp:8002', 'string')
									   ,'ReadKey'  => new xmlrpcval('XXXXX', 'string')
									   ,'WriteKey' => new xmlrpcval('XXXXX', 'string')
									   ,'AgentID'  => new xmlrpcval('61dfee5c-2440-49f7-8668-a47cecb19d04', 'string'))
									   , 'struct');

	$message = new xmlrpcmsg("groups.getAgentGroupMemberships", array($verifyParams));
	$resp = $client->send($message, 5);
	if ($resp->faultCode()) 
	{
		return array('error' => "Error validating AgentID and SessionID"
		           , 'xmlrpcerror'=> $resp->faultString()
				   , 'params' => var_export($params, TRUE));
	} 
			
	$verifyReturn = $resp->value();
	
			
	if( !isset($verifyReturn['auth_session']) || ($verifyReturn['auth_session'] != 'TRUE') )
	{
		return array('error' => "UserService.check_auth_session() did not return TRUE"
				   , 'params' => var_export($params, TRUE));
	
	}

?>
