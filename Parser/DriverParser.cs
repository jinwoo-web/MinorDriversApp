using System.Text;

namespace MinorDriversApp.Parser;

internal class DriverParser : TlvParser
{
    internal string 운전자구분 { get; set; } = string.Empty;
    internal string 면허색상 { get; set; } = string.Empty;
    internal string 유효기간 { get; set; } = string.Empty;
    internal List<string> 조건 { get; set; } = [];
    internal List<string> 비고 { get; set; } = [];
    internal string 면허번호 { get; set; } = string.Empty;
    internal string 면허연월일_1 { get; set; } = string.Empty;
    internal string 면허연월일_2 { get; set; } = string.Empty;
    internal string 면허연월일_3 { get; set; } = string.Empty;
    internal List<string> 종류 { get; set; } = [];

    internal byte[] 사진 { get; set; } = [];

    public DriverParser(byte[] data)
    {
        data = TrimEndZeros(data);
        List<Tlv> tlvs = [];

        for (int i = 0; i < data.Length; i++)
        {
            int lastTag = tlvs.LastOrDefault()?.Tag ?? 0x00;
            bool multiTag = lastTag > data[i];
            if (i + 2 > data.Length) break;

            int tag = multiTag ? data[i++] << 8 | data[i++] : data[i++];
            int length = data[i];
            if (length == 0x82)
            {
                length = data[++i] << 8 | data[++i];
            }

            byte[] value = new byte[length];

            try
            {
                Array.Copy(data, i + 1, value, 0, length);
            }
            catch
            {
                break;
            }

            tlvs.Add(new Tlv(tag, length, value));
            i += length;
        }

        foreach (var tlv in tlvs)
        {
            switch (tlv.Tag)
            {
                case 0xC3:
                    운전자구분 = GetStringFrom0208(tlv.Value);
                    break;

                case 0xC4:
                    운전자구분 = GetStringFrom0208(tlv.Value);
                    면허색상 = 운전자구분 switch
                    {
                        "優良" => "gold",
                        "新規" => "green",
                        "一般" => "blue",
                        _ => ""
                    };
                    break;

                case 0xC5:
                    유효기간 = Encoding.ASCII.GetString(tlv.Value);
                    break;

                case 0xC6:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xC7:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xC8:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xC9:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xCC:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xCD:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xCE:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xCF:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD0:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD1:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD2:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD3:
                    조건.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD7:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD8:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xD9:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xDA:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xDB:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xDC:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xDD:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xDE:
                    비고.Add(GetStringFrom0208(tlv.Value));
                    break;

                case 0xE7:
                    면허번호 = Encoding.ASCII.GetString(tlv.Value);
                    break;

                case 0xE9:
                    면허연월일_1 = Encoding.ASCII.GetString(tlv.Value);
                    break;

                case 0xEA:
                    면허연월일_2 = Encoding.ASCII.GetString(tlv.Value);
                    break;

                case 0xEB:
                    면허연월일_3 = Encoding.ASCII.GetString(tlv.Value);
                    break;

                case 0xEC:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A1");

                    break;

                case 0xED:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A4");

                    break;

                case 0xEE:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A5");

                    break;

                case 0xEF:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A6");

                    break;

                case 0xF0:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A7");

                    break;

                case 0xF1:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B1");

                    break;

                case 0xF2:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B2");

                    break;

                case 0xF3:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B7");

                    break;

                case 0xF4:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B3");

                    break;

                case 0xF5:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B5");

                    break;

                case 0xF6:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B6");

                    break;

                case 0xF7:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B7");

                    break;

                case 0xF8:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A2");

                    break;

                case 0xF9:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("B4");

                    break;

                case 0xFA:
                    if (tlv.Value[0] == 0x01)
                        종류.Add("A3");

                    break;

                case 0x0107:
                    사진 = tlv.Value;
                    break;
            }
        }
    }
}