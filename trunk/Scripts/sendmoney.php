<?php
error_reporting(E_ALL);
ini_set("display_errors", 1);

require("currency/include/config.php");
require("currency/include/env.mysql.php");
require("currency/include/opensim.mysql.php");

function do_call($host, $port, $uri, $request)
{
	$url = "";
	if ($uri!="") {
		$dec = explode(":", $uri);
		if (!strncasecmp($dec[0], "http", 4)) $url = "$dec[0]:$dec[1]";
	}
	if ($url=="") $url ="http://$host";
	$url = "$url:$port/";
  
	$header[] = "Content-type: text/xml";
	$header[] = "Content-length: ".strlen($request);

	$ch = curl_init();
	curl_setopt($ch, CURLOPT_URL, $url);
	curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
	curl_setopt($ch, CURLOPT_TIMEOUT, 1);
	curl_setopt($ch, CURLOPT_HTTPHEADER, $header);
	curl_setopt($ch, CURLOPT_POSTFIELDS, $request);

	$data = curl_exec($ch);
	if (!curl_errno($ch)) curl_close($ch);

	$ret = false;
	if ($data) $ret = xmlrpc_decode($data);

    
	return $ret;
}


function send_money($agentID, $amount, $secureID=null)
{
    if (!isGUID($agentID)) return false;

    if (!USE_CURRENCY_SERVER) {
    	env_set_money_transaction(null, $agentID, $amount, 5010, 0, "Add Money", 0, 0, "");
    	$res["success"] = true;

    	return $res;
	}


	//
	// XML RPC to Region Server
	//
	if (!isGUID($secureID, true)) return false;

	try{
    	$results = opensim_get_server_info($agentID);
	}catch(Exception $e){

	}
	if (!$results) return false;
	$serverip = $results["serverIP"];
	$httpport = $results["serverHttpPort"];
	$serveruri = $results["serverURI"];

	$results = opensim_get_avatar_session($agentID);
	//print_r($results);
	//$secureID = "e70f8d89-8b5e-499c-937a-21f1687d0931";
	//$sessionID = "c36c5928-c20e-427f-91c9-73eb773ec558";
	if (!$results) return false;
	$sessionID = $results["sessionID"];
	if ($secureID==null) $secureID = $results["secureID"];

	$req = array('bankerID'=>$agentID, 'bankerSessionID'=>$sessionID, 'bankerSecureSessionID'=>$secureID, 'amount'=>$amount);
	$params = array($req);
	$request = xmlrpc_encode_request('SendMoney', $params);
print("IP=".$serverip." PORT=".$httpport." URL=". $serveruri."<br />\n");
print_r($request);

	$response = do_call($serverip, $httpport, $serveruri, $request);
	print_r($response);
	return $response;
}
//@if true = ok
//@if empty = fail
echo send_money("751c1531-03b5-48a9-8f2d-51a0527be7ca",100);

?>
