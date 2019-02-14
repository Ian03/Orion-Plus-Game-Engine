﻿Imports System.IO
Imports System.IO.Compression
Imports System.Threading.Tasks

Friend Module L_Entry
    Private ReadOnly Win As New frmLauncher
    Private ReadOnly NewsLink As String = "https://onedrive.live.com/download?cid=9DFCEF1A8AABBCC4&resid=9DFCEF1A8AABBCC4%21112&authkey=AFtP2OAJbcU-TXw"
    Private ReadOnly PatchInfoLink As String = "https://onedrive.live.com/download?cid=9DFCEF1A8AABBCC4&resid=9DFCEF1A8AABBCC4%21113&authkey=AKU4ETFHsWCGMyA"

    <STAThread>
    Friend Sub Main()
        RunPatcher()
        Application.Run(Win)
    End Sub

    Private Async Sub RunPatcher()
        ' Generate Patch Path
        Dim path = Environment.CurrentDirectory & "/Patcher/"
        If Not Directory.Exists(path) Then
            Directory.CreateDirectory(path)
        End If

        ' Make sure we have a patch file
        Dim pathVersion = path & "version.info"
        If Not File.Exists(pathVersion) Then
            File.Create(pathVersion).Dispose()
        End If

        ' Get our version
        Dim version As Integer
        Using stream = New StreamReader(pathVersion)
            Integer.TryParse(stream.ReadLine(), version)
        End Using

        ' Get News
        If Not Await FetchNews() Then Return

        ' Get Patch Info
        Dim patchInfo = Await FetchPatchInfo()
        If Not patchInfo.Length > 0 Then Return

        ' Get Count
        Dim patchCount = CUInt(GetVar(patchInfo, "DEFAULT", "Count"))

        ' Perform our patches
        Do While version < patchCount
            Dim curIndex = version + 1
            Dim patchLink = GetVar(patchInfo, "PATCH" & curIndex, "SuperLink")

            If patchLink.Trim.Length > 0 Then
                If Not Await FetchPatch(path, patchLink, curIndex) Then Exit Do

                ' Update PatchInfo
                version = CUInt(GetVar(patchInfo, "PATCH" & curIndex, "IndexSkip"))
            Else
                patchLink = GetVar(patchInfo, "PATCH" & curIndex, "Link")
                If Not patchLink.Trim.Length > 0 Then
                    Win.UpdateNews("Patch #" & curIndex & " is currently broken." & Environment.NewLine &
                                   Environment.NewLine &
                                   "Please report this to the development team " & Environment.NewLine &
                                   "to have the problem corrected as soon as possible!")
                    Exit Do
                End If
                If Not Await FetchPatch(path, patchLink, curIndex) Then Exit Do

                ' Update PatchInfo
                version = curIndex
            End If
        Loop

        ' Update our patch version
        Using stream = New StreamWriter(pathVersion, False)
            stream.Write(version.ToString())
        End Using

        ' Allow play if up to date
        If version >= patchCount Then
            Win.EnableGame(GetVar(patchInfo, "DEFAULT", "App"), GetVar(patchInfo, "DEFAULT", "Code"))
        Else
            Win.UpdateNews("Failed to apply patches. Restart application to try again." &
                           Environment.NewLine &
                           "Contact a developer for assistence if problems continue!")
        End If
    End Sub

    Private Async Function FetchNews() As Task(Of Boolean)
        Try
            ' News Link
            Dim request = Net.WebRequest.Create(NewsLink)

            Using response As Net.HttpWebResponse = Await request.GetResponseAsync()
                Using stream As New StreamReader(response.GetResponseStream())
                    Win.SetNews(Await stream.ReadToEndAsync())
                End Using
            End Using
        Catch
            Win.SetNews("Error: Could Not reach update server. File missing or corrupted!")
            Return False
        End Try

        Return True
    End Function

    Private Async Function FetchPatchInfo() As Task(Of String())
        Dim patchInfo As String()

        Try
            ' Patch Link
            Dim request = Net.WebRequest.Create(PatchInfoLink)
            Using response As Net.HttpWebResponse = Await request.GetResponseAsync()
                Using stream As New StreamReader(response.GetResponseStream())
                    patchInfo = (Await stream.ReadToEndAsync()).Split(Environment.NewLine)
                End Using

                For i = 0 To patchInfo.Length - 1
                    patchInfo(i) = patchInfo(i).Trim
                Next
            End Using
        Catch
            ReDim patchInfo(0)
        End Try

        Return patchInfo
    End Function

    Private Async Function FetchPatch(path As String, patchLink As String, patchIndex As UInteger) As Task(Of Boolean)
        Dim patchPath = path & "Patch" & patchIndex.ToString() & ".zip"

        ' Delete Duplicate File(s)
        If (File.Exists(patchPath)) Then
            File.Delete(patchPath)
        End If

        Try
            ' Download patch
            Dim request = Net.WebRequest.Create(patchLink)

            Using response As Net.HttpWebResponse = Await request.GetResponseAsync()
                Using stream = response.GetResponseStream()
                    Dim fSize = response.ContentLength
                    Const bSize As Integer = 1024 * 1024
                    Dim buf As Byte() : ReDim buf(bSize)

                    Using f = New FileStream(patchPath, FileMode.OpenOrCreate)
                        Dim len = Await stream.ReadAsync(buf, 0, bSize)

                        While (len > 0)
                            f.Write(buf, 0, len)

                            ' Update form information
                            Dim progress = CInt(((f.Length / fSize) * 100))
                            If progress = 100 Then progress = 99
                            Win.UpdateStatus("Downloading Patch " & patchIndex & ". " &
                                             "Progress (" & progress & "%)")
                            Win.UpdateProgress(progress)
                            Application.DoEvents()

                            len = Await stream.ReadAsync(buf, 0, bSize)
                        End While
                    End Using
                End Using
            End Using

            ' Apply Patch
            If Not ApplyPatch(patchPath) Then
                Return False
            End If
        Catch e As Exception
            Win.UpdateNews(e.ToString())
            Return False
        End Try

        Return True
    End Function

    Private Function ApplyPatch(zip As String) As Boolean
        Try
            Using archive = ZipFile.OpenRead(zip)
                For Each srcFile As ZipArchiveEntry In archive.Entries
                    Dim destFile As String = Path.Combine(Environment.CurrentDirectory & "/", srcFile.FullName)
                    Dim dir As String = Path.GetDirectoryName(destFile)

                    If Not Directory.Exists(dir) Then
                        Directory.CreateDirectory(dir)
                    End If

                    If srcFile.Name <> "" Then
                        srcFile.ExtractToFile(destFile, True)
                    End If
                Next
            End Using
            File.Delete(zip)
        Catch
            Return False
        End Try

        ' Update form information
        Win.UpdateProgress(100)

        Return True
    End Function

    ' Helper Function
    Private Function GetVar(patchInfo As String(), header As String, name As String) As String
        Dim flag = False

        For i As Integer = 0 To patchInfo.Length - 1
            If flag AndAlso patchInfo(i).StartsWith(name & "=") Then
                Return patchInfo(i).Substring(name.Length + 1)
            End If
            If (patchInfo(i).StartsWith("[" & header & "]")) Then flag = True
        Next

        Return ""
    End Function

End Module