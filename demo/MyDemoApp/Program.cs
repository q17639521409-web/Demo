// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Text.RegularExpressions;

Console.WriteLine("Hello, World!");
string value = "&amp;lt;string&amp;gt;3&amp;lt;/string&amp;gt;";

if (string.IsNullOrEmpty(value))
{
    Console.WriteLine("IsNullOrEmpty");
}

string decoded = value;
while (true)
{
    string next = WebUtility.HtmlDecode(decoded);
    if (string.Equals(next, decoded, StringComparison.Ordinal))
    {
        string res = Regex.Replace(decoded, "</?[A-Za-z][^<>]*>", "");
        Console.WriteLine($"No more HTML entities to decode: {res}");
        break;
    }

    decoded = next;
}

