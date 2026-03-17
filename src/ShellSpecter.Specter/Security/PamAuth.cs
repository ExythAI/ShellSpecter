using System;
using System.Runtime.InteropServices;

namespace ShellSpecter.Specter.Security;

/// <summary>
/// P/Invoke wrapper for Linux PAM authentication.
/// Validates user credentials against the OS via libpam.so.
/// </summary>
public static class PamAuth
{
    private const string PamLibrary = "libpam.so.0";
    private const int PAM_SUCCESS = 0;
    private const int PAM_PROMPT_ECHO_OFF = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct pam_message
    {
        public int msg_style;
        public IntPtr msg;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct pam_response
    {
        public IntPtr resp;
        public int resp_retcode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct pam_conv
    {
        public PamConversationDelegate conv;
        public IntPtr appdata_ptr;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PamConversationDelegate(
        int num_msg,
        IntPtr msg,
        out IntPtr resp,
        IntPtr appdata_ptr);

    [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pam_start(string service_name, string user, ref pam_conv pam_conversation, out IntPtr pamh);

    [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pam_authenticate(IntPtr pamh, int flags);

    [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pam_end(IntPtr pamh, int pam_status);

    /// <summary>
    /// Authenticate a Linux user via PAM.
    /// On non-Linux platforms, always returns false.
    /// </summary>
    public static bool Authenticate(string username, string password)
    {
        if (!OperatingSystem.IsLinux())
            return false;

        IntPtr pamh = IntPtr.Zero;

        PamConversationDelegate pamConvDelegate = (int num_msg, IntPtr msg, out IntPtr resp, IntPtr appdata) =>
        {
            resp = Marshal.AllocHGlobal(IntPtr.Size * num_msg);

            for (int i = 0; i < num_msg; i++)
            {
                IntPtr pResponse = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(pam_response)));
                var responseStruct = new pam_response
                {
                    resp = Marshal.StringToHGlobalAnsi(password),
                    resp_retcode = 0
                };
                Marshal.StructureToPtr(responseStruct, pResponse, false);
                Marshal.WriteIntPtr(resp, i * IntPtr.Size, pResponse);
            }
            return PAM_SUCCESS;
        };

        var conv = new pam_conv { conv = pamConvDelegate, appdata_ptr = IntPtr.Zero };

        try
        {
            int startResult = pam_start("login", username, ref conv, out pamh);
            if (startResult != PAM_SUCCESS) return false;

            int authResult = pam_authenticate(pamh, 0);
            return authResult == PAM_SUCCESS;
        }
        finally
        {
            if (pamh != IntPtr.Zero)
            {
                pam_end(pamh, 0);
            }
        }
    }
}
