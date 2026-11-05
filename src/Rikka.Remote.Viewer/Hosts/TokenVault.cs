using System.Runtime.InteropServices;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace Rikka.Remote.Viewer.Hosts;

internal static unsafe class TokenVault
{
    private const string TargetPrefix = "Rikka Remote Viewer/";

    public static byte[]? Read(Pin pin)
    {
        CREDENTIALW* credential;
        fixed (char* target = Target(pin))
        {
            if (!PInvoke.CredRead(target, CRED_TYPE.CRED_TYPE_GENERIC, 0, &credential))
            {
                var error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
                if (error != WIN32_ERROR.ERROR_NOT_FOUND)
                {
                    Log.Warn($"cannot read the remembered sign-in: {error}");
                }

                return null;
            }
        }

        try
        {
            return new ReadOnlySpan<byte>(credential->CredentialBlob, (int)credential->CredentialBlobSize).ToArray();
        }
        finally
        {
            PInvoke.CredFree(credential);
        }
    }

    public static void Keep(Pin pin, string label, ReadOnlySpan<byte> token)
    {
        fixed (char* target = Target(pin))
        fixed (char* user = label)
        fixed (byte* blob = token)
        {
            var credential = new CREDENTIALW
            {
                Type = CRED_TYPE.CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = user,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)token.Length,
                Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
            };
            if (!PInvoke.CredWrite(&credential, 0))
            {
                Log.Warn($"cannot remember the sign-in: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
            }
        }
    }

    public static void Forget(Pin pin)
    {
        fixed (char* target = Target(pin))
        {
            if (!PInvoke.CredDelete(target, CRED_TYPE.CRED_TYPE_GENERIC, 0))
            {
                var error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
                if (error != WIN32_ERROR.ERROR_NOT_FOUND)
                {
                    Log.Warn($"cannot forget the sign-in: {error}");
                }
            }
        }
    }

    private static string Target(Pin pin) => TargetPrefix + pin.ToHex();
}
