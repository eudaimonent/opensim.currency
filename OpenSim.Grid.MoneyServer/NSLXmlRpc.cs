// * Copyright (c) Contributors, http://www.nsl.tuis.ac.jp/


using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Net;
using System.Text;
using System.Reflection;

using log4net;
using Nwc.XmlRpc;


namespace NSL.XmlRpc 
{

	public class NSLXmlRpcRequest : XmlRpcRequest
	{
		//private String _methodName = null;
		private Encoding _encoding = new ASCIIEncoding();
		private XmlRpcRequestSerializer _serializer = new XmlRpcRequestSerializer();
		private XmlRpcResponseDeserializer _deserializer = new XmlRpcResponseDeserializer();


		public NSLXmlRpcRequest()
      	{
      		_params = new ArrayList();
      	}


		public NSLXmlRpcRequest(String methodName, IList parameters)
		{
			MethodName = methodName;
			_params = parameters;
		}



		public XmlRpcResponse xSend(String url, Int32 timeout)
	  	{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

			if (request == null) {
				throw new XmlRpcException(XmlRpcErrorCodes.TRANSPORT_ERROR, 
						XmlRpcErrorCodes.TRANSPORT_ERROR_MSG +": Could not create request with " + url);
			}
			request.Method = "POST";
			request.ContentType = "text/xml";
			request.AllowWriteStreamBuffering = true;

			request.Timeout = timeout;
			request.Headers.Add("NoVerifyCert", "true");

			Stream stream = request.GetRequestStream();
			XmlTextWriter xml = new XmlTextWriter(stream, _encoding);
			_serializer.Serialize(xml, this);
			xml.Flush();
			xml.Close();

			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			StreamReader input = new StreamReader(response.GetResponseStream());

			XmlRpcResponse resp = (XmlRpcResponse)_deserializer.Deserialize(input);
			input.Close();
			response.Close();
			return resp;
	  	}

	}

}
