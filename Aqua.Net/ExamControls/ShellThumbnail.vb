Option Strict Off
Imports System.Drawing
Imports System.Runtime.InteropServices

''' <summary>
''' 透過 Windows Shell (IShellItemImageFactory) 取得檔案縮圖，
''' 與檔案總管顯示的縮圖相同，會使用系統已安裝的解碼器。
''' </summary>
Namespace Global.Aqua

Friend Module ShellThumbnail

    ' SIIGBF 旗標
    Private Const SIIGBF_RESIZETOFIT As Integer = &H0
    Private Const SIIGBF_BIGGERSIZEOK As Integer = &H1

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeSize
        Public cx As Integer
        Public cy As Integer
    End Structure

    <ComImport()>
    <Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IShellItemImageFactory
        Sub GetImage(<[In]()> size As NativeSize, flags As Integer, ByRef phbm As IntPtr)
    End Interface

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode, PreserveSig:=False)>
    Private Sub SHCreateItemFromParsingName(
        <MarshalAs(UnmanagedType.LPWStr)> path As String,
        pbc As IntPtr,
        <MarshalAs(UnmanagedType.LPStruct)> riid As Guid,
        <MarshalAs(UnmanagedType.Interface)> ByRef ppv As IShellItemImageFactory)
    End Sub

    <DllImport("gdi32.dll")>
    Private Function DeleteObject(hObject As IntPtr) As Boolean
    End Function

    Private ReadOnly IID_IShellItemImageFactory As New Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")

    ''' <summary>
    ''' 取得指定檔案的縮圖。失敗時回傳 Nothing（呼叫端需自行處理）。
    ''' 回傳的 Bitmap 由呼叫端負責 Dispose。
    ''' </summary>
    Public Function GetThumbnail(path As String, width As Integer, height As Integer) As Bitmap
        If String.IsNullOrEmpty(path) Then Return Nothing
        If width <= 0 Then width = 256
        If height <= 0 Then height = 256

        Dim factory As IShellItemImageFactory = Nothing
        Dim hBitmap As IntPtr = IntPtr.Zero
        Try
            SHCreateItemFromParsingName(path, IntPtr.Zero, IID_IShellItemImageFactory, factory)
            If factory Is Nothing Then Return Nothing

            Dim sz As New NativeSize With {.cx = width, .cy = height}
            factory.GetImage(sz, SIIGBF_RESIZETOFIT Or SIIGBF_BIGGERSIZEOK, hBitmap)
            If hBitmap = IntPtr.Zero Then Return Nothing

            ' 從 HBITMAP 複製出一份受管理的 Bitmap
            Using shellBmp As Bitmap = Bitmap.FromHbitmap(hBitmap)
                Return New Bitmap(shellBmp)
            End Using
        Catch
            ' 縮圖取得失敗（不支援的格式、檔案損毀等）→ 回傳 Nothing
            Return Nothing
        Finally
            If hBitmap <> IntPtr.Zero Then DeleteObject(hBitmap)
            If factory IsNot Nothing Then Marshal.ReleaseComObject(factory)
        End Try
    End Function

End Module

End Namespace
