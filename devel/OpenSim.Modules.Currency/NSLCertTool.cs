/* 
 * Copyright (c) Contributors, http://www.nsl.tuis.ac.jp
 *
 */


using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

using log4net;
//using Nwc.XmlRpc;



namespace NSL.Network.CertTool 
{
	public static class NSLCertVerify
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static X509Certificate2 m_cert = null;
		public static X509Chain m_chain = null;


		public static void setCA(string certfile)
	  	{
			m_cert  = new X509Certificate2(certfile);
            m_chain = new X509Chain();

			m_chain.ChainPolicy.ExtraStore.Add(m_cert);
			m_chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            m_chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
	  	}


		public static bool checkCert(X509Certificate2 cert)
		{
			if (m_chain==null || m_cert==null) {
				return false;
			}

            bool ret = m_chain.Build(cert);
			if (ret) return true;

            for (int i=0; i<m_chain.ChainStatus.Length; i++)  {
				if (m_chain.ChainStatus[i].Status==X509ChainStatusFlags.UntrustedRoot) return true;
			}

			return false;
		}



/*
        RemoteCertificateNotAvailable = 1, // 証明書が利用できません。
        RemoteCertificateNameMismatch = 2, // 証明書名が不一致です。
        RemoteCertificateChainErrors = 4,  // ChainStatus が空でない配列を返しました。
*/
		public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (sender is HttpWebRequest) {
				//
				HttpWebRequest Request = (HttpWebRequest)sender;

				if (Request.Headers.Get("NoVerifyCert")=="true") {
					return true;
				}

				if (sslPolicyErrors!=SslPolicyErrors.RemoteCertificateChainErrors) {
					return false;
				}

				bool valid = checkCert((X509Certificate2)certificate);
				if (!valid) {
					m_log.InfoFormat("[NSL CERT VERIFY] Failed to verify certification.");
				}
				return valid;
			}

			//
			else if (sslPolicyErrors!=SslPolicyErrors.RemoteCertificateChainErrors) { 
				return false;
			}

			return true;
		}

	}



	public class NSLCertPolicy : ICertificatePolicy
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
