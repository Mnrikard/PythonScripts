using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;



namespace Comic
{
	public class Reader
	{
		public static void Main()
		{
			StringBuilder emailBody = new StringBuilder();
			Xkcd xkcd = new Xkcd();
			DinoComic dinocomic = new DinoComic();
			Mtts mtts = new Mtts();

			emailBody.Append(dinocomic.Read());
			emailBody.AppendLine("<hr />");
			emailBody.Append(xkcd.Read());
			emailBody.AppendLine("<hr />");
			emailBody.Append(mtts.Read());
			emailBody.AppendLine("<hr />");
			Console.WriteLine(emailBody.ToString());
			SendMailBody(emailBody.ToString());
		}
		private static void SendMailBody(string body)
		{
			MailMessage mm = new MailMessage("mnrikard@gmail.com","mnrikard@gmail.com","Daily Comic",body);
			mm.IsBodyHtml = true;
			SmtpClient client = new SmtpClient("smtp.gmail.com");
			client.Port = 25;
			client.EnableSsl = true;
			client.Credentials = new NetworkCredential("mnrikard@gmail.com", "IwRR@n&ped");
			client.Send(mm);
		
		}
	}

	public interface IComic
	{
		string Read();
	}
	
	public abstract class Comic : IComic
	{
		public virtual string Read()
		{
			throw new Exception("do not use abstract class");
		}
		protected string ReadHtml(string url)
		{
			HttpWebRequest Req = (HttpWebRequest)HttpWebRequest.Create(url);
			HttpWebResponse Resp = (HttpWebResponse)Req.GetResponse();
			StreamReader oSRead = new StreamReader(Resp.GetResponseStream());
			string op = oSRead.ReadToEnd();
			oSRead.Close();
			return op;			
		}
	}

	public class Mtts : Comic {
		private string _comicText;
		public override string Read() {
			string img = ExtractImageFromComic();
			string etext = ExtractExtraTextFromImage();
			return String.Format("{0}{1}", img, etext);
		}

		protected string ExtractImageFromComic() {
			string comicHtml = ReadHtml("http://www.marriedtothesea.com/");

			comicHtml = Regex.Replace(comicHtml, "(.|\n)+?<div id=\"comicarea\">", String.Empty);
			comicHtml = Regex.Replace(comicHtml, "(.|\n)+?<div id=\"butts\"(.|\n)+?<img", "<img", RegexOptions.IgnoreCase);
			Match m = Regex.Match(comicHtml, "<img(.|\n)+?>", RegexOptions.IgnoreCase);
			return m.Value;
		}
		protected string ExtractExtraTextFromImage() {
			return String.Empty;
		}

	}

	public class Xkcd : Comic
	{
		private string _comicText;
		public override string Read()
		{
			string img = ExtractImageFromComic();
			string etext = ExtractExtraTextFromImage();
			return String.Format("{0}{1}",img,etext);
		}

		protected string ExtractImageFromComic()
		{
			string comicHtml = ReadHtml("http://www.xkcd.com");
			Match m = Regex.Match(comicHtml,@"div id=""comic""[\d\D]+?(?'url'<img.+?>)",RegexOptions.IgnoreCase);
			string output = m.Groups["url"].Value;
			_comicText = Regex.Match(output,@"title\s*=\s*""(?'title'.+?)""",RegexOptions.IgnoreCase).Groups["title"].Value;
			return output;
		}
		protected string ExtractExtraTextFromImage()
		{
			return "<ul><li>"+_comicText+"</li></ul>";
		}	

	}
	public class DinoComic : Comic
	{
		private string _comicText;
		private string _comicHtml;
		public override string Read()
		{
			string img = ExtractImageFromComic();
			string etext = ExtractExtraTextFromImage();
			return String.Format("{0}{1}",img,etext);
		}

		protected string ExtractImageFromComic()
		{
			_comicHtml = ReadHtml("http://www.qwantz.com/index.php");
			
			//Match m = Regex.Match(_comicHtml,@"<span class=""rss-title"">klassic komix!</span>[\d\D]+?(?'url'<img.+?>)",RegexOptions.IgnoreCase);
			Match m = Regex.Match(_comicHtml,@"<img src=""(?'url'[^""]+?\.png)"" class=""comic"" title=""(?'comment'.+?)""",RegexOptions.IgnoreCase);


			string output = m.Value + " />";
			_comicText = m.Groups["comment"].Value;
			return output;

		}
		protected string ExtractExtraTextFromImage()
		{
			string mailtoText = Regex.Match(_comicHtml,@"mailto:ryan@qwantz.com\?subject=(?'et'(.)+?)""",RegexOptions.IgnoreCase).Groups["et"].Value;

			XmlDocument rss = new XmlDocument();
			rss.Load("http://www.rsspect.com/rss/qwantz.xml");
			XmlNode firstItem = rss.SelectSingleNode("//item/title");
			string rssText = firstItem.InnerText;


			return String.Format("<ul><li>{0}</li><li>\r\n{1}</li><li>{2}</li></ul>", _comicText, mailtoText, rssText);
		}	

	}
}

