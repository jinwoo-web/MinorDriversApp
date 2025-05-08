using System.Text;

namespace MinorDriversApp.Parser;

internal class PersonalParser : TlvParser
{
    internal string 이름 { get; set; } = string.Empty;
    internal string 주소 { get; set; } = string.Empty;
    internal string 생년월일 { get; set; } = string.Empty;
    internal string 성별 { get; set; } = string.Empty;

    public PersonalParser(byte[] data)
    {
        data = TrimEndZeros(data);
        var flvs1 = Parse(data, true);

        foreach (var flv1 in flvs1)
        {
            var flvs2 = Parse(flv1.Value, true);

            foreach (var flv2 in flvs2)
            {
                switch (flv2.Tag)
                {
                    case 0xDF22:
                        이름 = Encoding.UTF8.GetString(flv2.Value).Replace('\u3000', ' ');
                        break;

                    case 0xDF23:
                        주소 = Encoding.UTF8.GetString(flv2.Value).Replace('\u3000', ' ');
                        break;

                    case 0xDF24:
                        생년월일 = Encoding.ASCII.GetString(flv2.Value);
                        break;

                    case 0xDF25:
                        성별 = Encoding.ASCII.GetString(flv2.Value);
                        break;
                }
            }
        }
    }
}
