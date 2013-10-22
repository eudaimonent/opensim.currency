/* 
 * Copyright (c) Contributors, http://www.nsl.tuis.ac.jp
 *
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

using log4net;



namespace NSL.Certificate.Tools 
{
	//
	public class NSLCertificateVerify
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private X509Certificate2 m_cacert = null;
		private X509Chain m_chain = null;


        public NSLCertificateVerify()
        {
			m_cacert = null;
			m_chain  = null;
        }


        public NSLCertificateVerify(string certfile)
        {
            SetPrivateCA(certfile);
        }


		public void SetPrivateCA(string certfile)
	  	{
			m_cacert = new X509Certificate2(certfile);
            m_chain  = new X509Chain();

			m_chain.ChainPolicy.ExtraStore.Add(m_cacert);
			m_chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            m_chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
	  	}


/*
		public void SetPrivateCA(string pfxfile, string passwd)
	  	{
			X509Certificate2 cert = new X509Certificate2(pfxfile, passwd);
			byte[] bytes = cert.Export(X509ContentType.Cert, passwd);

			m_cacert = new X509Certificate2(bytes);
            m_chain  = new X509Chain();

			m_chain.ChainPolicy.ExtraStore.Add(m_cacert);
			m_chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            m_chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
	  	}
*/


        //
        //
		//
		public bool CheckPrivateChain(X509Certificate2 cert)
		{
			if (m_chain==null || m_cacert==null) {
				return false;
			}

            bool ret = m_chain.Build((X509Certificate2)cert);
			if (ret) {
				return true;
			}

            for (int i=0; i<m_chain.ChainStatus.Length; i++)  {
				if (m_chain.ChainStatus[i].Status==X509ChainStatusFlags.UntrustedRoot) return true;
			}
			//
			return false;
		}



		/*
		SslPolicyErrors:
        	RemoteCertificateNotAvailable = 1, // 証明書が利用できません。
        	RemoteCertificateNameMismatch = 2, // 証明書名が不一致です。
        	RemoteCertificateChainErrors  = 4, // ChainStatus が空でない配列を返しました。
		*/

		//
		//
		//
        public bool ValidateServerCertificate(object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			m_log.InfoFormat("[NSL CERT VERIFY]: ValidateServerCertificate: Policy is ({0})", sslPolicyErrors);

			if (obj is HttpWebRequest) {
				//
				HttpWebRequest Request = (HttpWebRequest)obj;
				string noVerify = Request.Headers.Get("NoVerifyCert");
				if (noVerify!=null && noVerify.ToLower()=="true") {
					return true;
				}
			}

			X509Certificate2 certificate2 = new X509Certificate2(certificate);
            string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);

			// None, ChainErrors 以外は全てエラーとする．
			if (sslPolicyErrors!=SslPolicyErrors.None && sslPolicyErrors!=SslPolicyErrors.RemoteCertificateChainErrors) {
				m_log.InfoFormat("[NSL CERT VERIFY]: ValidateServerCertificate: Simple Name is \"{0}\"", simplename);
				m_log.InfoFormat("[NSL CERT VERIFY]: ValidateServerCertificate: Policy Error!", sslPolicyErrors);
				return false;
			}

			//X509Certificate2 certificate2 = new X509Certificate2(certificate);
            //string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);
 
			bool valid = CheckPrivateChain(certificate2);
			if (valid) {
                m_log.InfoFormat("[NSL CERT VERIFY]: Valid Server Certification for \"{0}\"", simplename);
			}
			else {
				m_log.InfoFormat("[NSL CERT VERIFY]: Failed to Verify Server Certification for \"{0}\"", simplename);
			}
			return valid;
		}


		//
		//
		// obj is SslStream
		public bool ValidateClientCertificate(object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			m_log.InfoFormat("[NSL CERT VERIFY]: ValidateClientCertificate: Policy is ({0})", sslPolicyErrors);

			X509Certificate2 certificate2 = new X509Certificate2(certificate);
            string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);

			// None, ChainErrors 以外は全てエラーとする．
			if (sslPolicyErrors!=SslPolicyErrors.None && sslPolicyErrors!=SslPolicyErrors.RemoteCertificateChainErrors) {
				m_log.InfoFormat("[NSL CERT VERIFY]: ValidateClientCertificate: Simple Name is \"{0}\"", simplename);
				m_log.InfoFormat("[NSL CERT VERIFY]: ValidateClientCertificate: Policy Error!");
				return false;
			}

			//X509Certificate2 certificate2 = new X509Certificate2(certificate);
            //string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);

			bool valid = CheckPrivateChain(certificate2);
			if (valid) {
                m_log.InfoFormat("[NSL CERT VERIFY]: Valid Client Certification for \"{0}\"", simplename);
 			}
			else {
                m_log.InfoFormat("[NSL CERT VERIFY]: Failed to Verify Client Certification for \"{0}\"", simplename);
			}
			return valid;
		}

	}



    //
    public class NSLCertificatePolicy : ICertificatePolicy
	{
//		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem)
		{
			if (certificateProblem == 0 || 				//正常
				certificateProblem == -2146762487 || 	//信頼されてない？
				certificateProblem == -2146762495 || 	//期限切れ
				certificateProblem == -2146762481) {	//名前不正？
				return true;
			}
			else {
				return false;
			}
		}
	}


}
