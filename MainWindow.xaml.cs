using PCSC.Monitoring;
using PCSC;
using System.Windows;
using PCSC.Utils;
using System.Reflection;
using MinorDriversApp.Models;
using System.Diagnostics;
using PCSC.Iso7816;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using MinorDriversApp.Parser;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;

namespace MinorDriversApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IContextFactory ContextFactory = PCSC.ContextFactory.Instance;
    private readonly IMonitorFactory MonitorFactory = PCSC.Monitoring.MonitorFactory.Instance;

    private static readonly byte[] RID = [0xA0, 0x00, 0x00, 0x02, 0x31];
    private static readonly byte[] AP_AID = [0xD3, 0x92, 0x10, 0x00, 0x31, 0x00, 0x01, 0x01, 0x04, 0x08];
    private static readonly byte[] AP_INSTANCE = [.. RID, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    private MainViewModel vm = new();

    private record ResponseData
    {
        public byte[] EF001A { get; set; } = [];
        public byte[] EF001B { get; set; } = [];
        public byte[] EF001C { get; set; } = [];
        public byte[] EF0002 { get; set; } = [];
        public byte[] EF0003 { get; set; } = [];
        public byte[] EF0004 { get; set; } = [];
        public byte[] EF0007 { get; set; } = [];
    }

    TaskCompletionSource? cardInsertCompletion;

    public MainWindow()
    {
        CoreJ2K.Util.WindowsBitmapImageCreator.Register();

        InitializeComponent();

        DataContext = vm;
    }

    private async void BtnRead_Click(object sender, RoutedEventArgs e)
    {
        cardInsertCompletion = new TaskCompletionSource();

        try
        {
            if (string.IsNullOrEmpty(vm.Pin1) || string.IsNullOrEmpty(vm.Pin2))
                throw new Exception("PIN1またはPIN2はありません。"); // PIN1 or PIN2 is null

            using ISCardContext context = ContextFactory.Establish(SCardScope.System);
            using ISCardMonitor monitor = MonitorFactory.Create(SCardScope.System);
            var readerName = context.GetReaders().FirstOrDefault() ?? throw new Exception("SDリーダーが見つかりません。");
            Debug.WriteLine($"Reader: {readerName}");

            monitor.Start(readerName);
            monitor.CardInserted += (_, _) => cardInsertCompletion.TrySetResult();

            var readerState = context.GetReaderStatus(readerName);
            SCardError error = context.GetStatusChange(1000, [readerState]);
            if (error != SCardError.Success)
                throw new Exception(SCardHelper.StringifyError(error));

            if ((readerState.EventState & SCRState.Present) != SCRState.Present)
                await cardInsertCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var responseData = ReadSCard(context, readerName, vm.Pin1, vm.Pin2);

            var driver = new DriverParser(responseData.EF001B);
            var personal = new PersonalParser(responseData.EF0002);

            var face = CoreJ2K.J2kImage.FromBytes(driver.사진);
            using var faceBmp = face.As<Bitmap>();
        }
        catch (OperationCanceledException)
        {
            throw new Exception("カードリーダーの操作がキャンセルされました。"); // 카드 리더기 작업이 취소되었습니다.
        }
        catch (TimeoutException)
        {
            throw new Exception("カードリーダーの操作がタイムアウトしました。"); // 카드 리더기 작업이 타임아웃되었습니다.
        }
    }

    private ResponseData ReadSCard(ISCardContext context, string readerName, string pin1, string pin2)
    {
        ResponseData responseData = new();
        using var reader = new SCardReader(context);
        SCardError error = reader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        if (error != SCardError.Success)
            throw new Exception(SCardHelper.StringifyError(error));

        using var isoReader = new IsoReader(context, readerName, SCardShareMode.Shared, SCardProtocol.Any, false);

        // 1. AID_APP 선택
        var response = SelectAID(isoReader, AP_AID);
        ThrowIfException(response, "AID_APP Select");

        // 2. AID_INSTANCE 선택
        response = SelectAID(isoReader, AP_INSTANCE);
        ThrowIfException(response, "AID_INSTANCE Select");

        // 3. EF001A 선택 및 읽기
        response = SelectFile(isoReader, 0x001A);
        ThrowIfException(response, "EF001A Select");

        response = ReadBinary(isoReader, 0x9A00, false);
        ThrowIfException(response, "EF001A ReadBinary");
        responseData.EF001A = response.GetData();

        // 4. EF0006 선택
        response = SelectFile(isoReader, 0x0006);
        ThrowIfException(response, "EF0006 Select");

        // 5. PIN1 인증
        response = Verify(isoReader, 0x86, pin1);
        ThrowIfException(response, "Verify PIN1");

        // 6. EF001B 선택 및 읽기
        response = SelectFile(isoReader, 0x001B);
        ThrowIfException(response, "EF001B Select");

        response = ReadBinary(isoReader, 0x9B00);
        ThrowIfException(response, "EF001B ReadBinary");
        responseData.EF001B = response.GetData();

        // 7. EF001C 선택 및 읽기
        response = SelectFile(isoReader, 0x001C);
        ThrowIfException(response, "EF001C Select");

        response = ReadBinary(isoReader, 0x9C00);
        ThrowIfException(response, "EF001C ReadBinary");
        responseData.EF001C = response.GetData();

        // 8. AID_APP 재선택
        response = SelectAID(isoReader, AP_AID);
        ThrowIfException(response, "AID_APP Select");

        // 9. EF0015 선택
        response = SelectFile(isoReader, 0x0015);
        ThrowIfException(response, "EF0015 Select");

        // 10. PIN2 인증
        response = Verify(isoReader, 0x95, pin2);
        ThrowIfException(response, "Verify PIN2");

        // 11. EF0007 선택 및 읽기
        response = SelectFile(isoReader, 0x0007);
        ThrowIfException(response, "EF0007 Select");

        response = ReadBinary(isoReader, 0x8700);
        ThrowIfException(response, "EF0007 ReadBinary");
        responseData.EF0007 = response.GetData();

        // 12. EF0004 선택 및 읽기
        response = SelectFile(isoReader, 0x0004);
        ThrowIfException(response, "EF0004 Select");

        response = ReadBinary(isoReader, 0x8400);
        ThrowIfException(response, "EF0004 ReadBinary");
        responseData.EF0004 = response.GetData();

        // 13. EF0002 선택 및 읽기
        response = SelectFile(isoReader, 0x0002);
        ThrowIfException(response, "EF0002 Select");

        response = ReadBinary(isoReader, 0x8200);
        ThrowIfException(response, "EF0002 ReadBinary");
        responseData.EF0002 = response.GetData();

        // 14. EF0003 선택 및 읽기
        response = SelectFile(isoReader, 0x0003);
        ThrowIfException(response, "EF0003 Select");

        response = ReadBinary(isoReader, 0x8300);
        ThrowIfException(response, "EF0003 ReadBinary");
        responseData.EF0003 = response.GetData();

        var isVerify = SignatureVerify(isoReader, responseData);
        if (!isVerify)
            throw new Exception("署名の検証に失敗しました。"); // 서명 검증 실패

        return responseData;
    }

    private bool SignatureVerify(IsoReader isoReader, ResponseData responseData)
    {
        var data = responseData.EF0002
            .Concat(responseData.EF001B)
            .Concat(Encoding.ASCII.GetBytes(DateTime.Now.Ticks.ToString()))
            .ToArray();

        var hash = SHA256.HashData(data);
        var oid = new Oid("2.16.840.1.101.3.4.2.1");

        if (oid.Value is null)
            throw new Exception("OIDを取得できません。"); // OID를 가져올 수 없습니다.

        // ASN.1 구성
        AsnWriter write = new(AsnEncodingRules.DER);
        write.PushSequence();
        write.PushSequence();
        write.WriteObjectIdentifier(oid.Value);
        write.WriteNull();
        write.PopSequence();
        write.WriteOctetString(hash);
        write.PopSequence();

        var digestInfo = write.Encode();
        Response response = isoReader.Transmit(new CommandApdu(IsoCase.Case4Short, isoReader.ActiveProtocol)
        {
            CLA = 0x80,
            INS = 0x2A,
            P1 = 0x00,
            P2 = 0x00,
            Data = digestInfo,
            Le = 0x00
        });
        ThrowIfException(response, "Signature");

        byte[] signature = response.GetData();
        byte[] publicKey = GetPublicKey(responseData.EF0007);
        using RSA rsa = RSA.Create(new RSAParameters
        {
            Modulus = publicKey,
            Exponent = [0x01, 0x00, 0x01]
        });

        return rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private Response SelectAID(IsoReader isoReader, byte[] data)
    {
        return isoReader.Transmit(new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
        {
            CLA = 0x00,
            INS = 0xA4,
            P1 = 0x04,
            P2 = 0x0C,
            Data = data
        });
    }

    private Response SelectFile(IsoReader isoReader, ushort fileId)
    {
        return isoReader.Transmit(new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
        {
            CLA = 0x00,
            INS = 0xA4,
            P1 = 0x02,
            P2 = 0x0C,
            Data = [(byte)(fileId >> 8), (byte)(fileId & 0xFF)]
        });
    }

    private Response Verify(IsoReader isoReader, byte p2, string pin)
    {
        return isoReader.Transmit(new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
        {
            CLA = 0x00,
            INS = 0x20,
            P1 = 0x00,
            P2 = p2,
            Data = Encoding.ASCII.GetBytes(pin)
        });
    }

    private Response ReadBinary(IsoReader isoReader, ushort offset, bool isExtended = true)
    {
        var isoCase = isExtended ? IsoCase.Case2Extended : IsoCase.Case2Short;

        return isoReader.Transmit(new CommandApdu(isoCase, isoReader.ActiveProtocol)
        {
            CLA = 0x00,
            INS = 0xB0,
            P1 = (byte)(offset >> 8),
            P2 = (byte)(offset & 0xFF),
            Le = isExtended ? 0x0000 : 0x00
        });
    }

    private void ThrowIfException(Response response, string description)
    {
        if (response is { SW1: 0x90, SW2: 0x00 }) return;

        var sw1 = response.SW1;
        var sw2 = response.SW2;

        var message = sw1 switch
        {
            0x62 => sw2 switch
            {
                0x81 => "出力データに異常がある",
                0x83 => "DFが閉塞(へいそく)している",
                _ => "警告処理。不揮発性メモリの状態が変化していない"
            },
            0x63 => sw2 switch
            {
                0x00 => "照合不一致である",
                0x81 => "ファイルが今回の書き込みによっていっぱいになった",
                >= 0xC0 and <= 0xCF => "照合不一致である。'n'によって、残りの再試行回数(1～15)を示す。",
                _ => "警告処理。不揮発性メモリの状態が変化している"
            },
            0x64 => sw2 switch
            {
                0x00 => "ファイル制御情報に異常がある",
                _ => "不揮発性メモリの状態が変化していない"
            },
            0x65 => sw2 switch
            {
                0x00 => "メモリへの書き込みが失敗した",
                _ => "不揮発性メモリの状態が変化していない"
            },
            0x67 => sw2 switch
            {
                0x00 => "Lc/Leフィールドが間違っている",
                _ => "不明なエラー"
            },
            0x68 => sw2 switch
            {
                0x81 => "指定された論理チャンネル番号によるアクセス機能を提供しない",
                0x82 => "CLAバイトで指定されたセキュアメッセージング機能を提供しない",
                _ => "CLAの機能が提供されない"
            },
            0x69 => sw2 switch
            {
                0x81 => "ファイル構造と矛盾したコマンドである",
                0x82 => "セキュリティステータスが満足されない",
                0x83 => "認証方法を受け付けない",
                0x84 => "参照されたIEFが閉塞している",
                0x85 => "コマンドの使用条件が満足されない",
                0x86 => "ファイルが存在しない",
                0x87 => "セキュアメッセージングに必要なデータオブジェクトが存在しない",
                0x88 => "セキュアメッセージング関連エラー",
                _ => "コマンドは許されない"
            },
            0x6A => sw2 switch
            {
                0x80 => "データフィールドのタグが正しくない",
                0x81 => "機能が提供されていない",
                0x82 => "ファイルが存在しない",
                0x83 => "アクセス対象のレコードがない",
                0x84 => "ファイル内に十分なメモリ容量がない",
                0x85 => "Lcの値がTLV構造に矛盾している",
                0x86 => "P1 - P2の値が正しくない",
                0x87 => "Lcの値がP1 - P2に矛盾している",
                0x88 => "参照された鍵が正しく設定されていない",
                _ => "間違ったパラメータP1,P2"
            },
            0x6B => sw2 switch
            {
                0x00 => "EF範囲外にオフセット指定した",
                _ => null
            },
            0x6D => sw2 switch
            {
                0x00 => "INSが提供されていない",
                _ => null
            },
            0x6E => sw2 switch
            {
                0x00 => "CLAが提供されていない",
                _ => null
            },
            0x6F => sw2 switch
            {
                0x00 => "自己診断異常",
                _ => null
            },
            _ => null
        };

        message ??= "不明なエラー";

        throw new Exception($"{description} - {message}"); // 카드 리더기 오류
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        cardInsertCompletion?.TrySetCanceled();
    }

    private byte[] GetPublicKey(byte[] data)
    {
        var flvs1 = TlvParser.Parse(data, true);
        foreach (var flv1 in flvs1)
        {
            var flvs2 = TlvParser.Parse(flv1.Value, true);

            foreach (var flv2 in flvs2)
            {
                var flvs3 = TlvParser.Parse(flv2.Value, false);

                foreach (var flv3 in flvs3)
                {
                    switch (flv3.Tag)
                    {
                        case 0x91:
                            return flv3.Value;
                    }
                }
            }
        }

        throw new Exception("証明書の公開鍵が見つかりません。"); // 인증서의 공개키를 찾을 수 없습니다.
    }
}