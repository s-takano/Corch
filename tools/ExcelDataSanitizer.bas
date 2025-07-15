Attribute VB_Name = "WorkbookSanitizer"
'============================================
'  Module : SanitizeWorkbookInPlace
'============================================
Option Explicit

'--------------------------------------------------
' Entry point 
'--------------------------------------------------
Public Sub SanitizeWorkbookInPlace()
    Dim ws As Worksheet
    
    Randomize Timer        'fresh pseudonyms every run
    
    For Each ws In ThisWorkbook.Worksheets
        'If you created *_Sanitised sheets earlier and want to
        'leave them untouched, uncomment the next line:
        'If Right$(ws.Name, 10) = "_Sanitised" Then GoTo NextSheet
        
        SanitizeSheetInPlace ws
        
NextSheet:
    Next ws
    
    MsgBox "All sheets sanitised in place.", vbInformation
End Sub

'--------------------------------------------------
' Sanitize a single sheet (in place)
'--------------------------------------------------
Private Sub SanitizeSheetInPlace(ws As Worksheet)
    Dim lastRow As Long, lastCol As Long
    Dim r As Long, c As Long, hdr As String, rule As String
    
    'identify contiguous data block (header in row 1)
    lastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    lastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    
    'loop over every data cell
    For c = 1 To lastCol
        hdr = ws.Cells(1, c).Value
        rule = LookupRule(hdr)
        
        For r = 2 To lastRow
            ws.Cells(r, c).Value = _
                ApplyRule(ws.Cells(r, c).Value, rule, r)
        Next r
    Next c
End Sub


'--------------------------------------------------
' Dispatch rules  (additions in **bold**)
'--------------------------------------------------
Private Function ApplyRule(valIn As Variant, rule As String, rowIdx As Long) As Variant
    
    If IsError(valIn) Then
        ApplyRule = valIn     'propagate Excel errors unchanged
        Exit Function
    End If
    
    Select Case LCase(rule)
        Case "mask_name"
            ApplyRule = "氏名_" & Format(rowIdx, "00000")
        
        Case "mask_phone"
            ApplyRule = "090-" & Format(10000000 + Int(89999999 * Rnd), "00000000")
        
        Case "mask_post"
            ApplyRule = Format(1600000 + Int(9999 * Rnd), "000\-0000")
        
        Case "mask_email"
            ApplyRule = "user" & Format(rowIdx, "00000") & "@example.com"
        
        Case "shift_date"
            If IsDate(valIn) Then _
                ApplyRule = CDate(valIn) + Int(60 * Rnd) - 30   '±30-day jitter
        
        '――― NEW handlers ―――
        Case "mask_id"
            ApplyRule = "ID" & Format(rowIdx, "00000")
        
        Case "mask_note"
            ApplyRule = "備考_" & Format(rowIdx, "00000")
        
        Case "mask_addr"
            ApplyRule = "東京都サンプル" & Format(rowIdx, "00000")
        
        '――― Pass-through options ―――
        Case "leave", ""
            ApplyRule = valIn
        
        Case Else
            'safety fallback: copy unchanged
            ApplyRule = valIn
    End Select
End Function

'--------------------------------------------------
' Build once-per-run dictionary:  Header → Rule
'--------------------------------------------------
Private Function RuleDict() As Object
    Dim d As Object
    Set d = CreateObject("Scripting.Dictionary")   ' late binding; no reference needed
    d.CompareMode = vbTextCompare                  ' case-insensitive keys
    
    '――― Primary parties ―――
    d.Add "契約者_名", "mask_name"
    d.Add "入居者1_名", "mask_name"
    d.Add "連帯保証人1_名", "mask_name"
    d.Add "緊急連絡先_名", "mask_name"
    
    '――― IDs / notes ―――
    d.Add "保証会社ID", "mask_id"
    d.Add "備考1", "mask_note"
    d.Add "備考2", "mask_note"
    d.Add "契約者_カナ", "mask_name"
    d.Add "契約者名", "mask_name"
    
    '――― Contract-holder contact ―――
    d.Add "契約者_郵便番号", "mask_post"
    d.Add "契約者_住所", "mask_addr"
    d.Add "契約者_電話番号", "mask_phone"
    d.Add "契約者_携帯電話番号", "mask_phone"
    d.Add "契約者_メールアドレス", "mask_email"
    d.Add "契約者_生年月日", "shift_date"
    d.Add "契約者_勤務先名", "mask_name"
    d.Add "契約者_勤務先電話番号", "mask_phone"
    
    '――― Guarantor ―――
    d.Add "連帯保証人1_郵便番号", "mask_post"
    d.Add "連帯保証人1_住所", "mask_addr"
    d.Add "連帯保証人1_電話番号", "mask_phone"
    d.Add "連帯保証人1_メールアドレス", "mask_email"
    d.Add "連帯保証人1_勤務先名", "mask_name"
    d.Add "連帯保証人1_勤務先電話番号", "mask_phone"
    
    '――― Emergency contact ―――
    d.Add "緊急連絡先_郵便番号", "mask_post"
    d.Add "緊急連絡先_住所", "mask_addr"
    d.Add "緊急連絡先_電話番号", "mask_phone"
    
    d.Add "上司報告者", "mask_name"
    d.Add "転出点検者", "mask_name"
    d.Add "精算書作成者", "mask_name"
    d.Add "精算書送付者", "mask_name"
    
    Set RuleDict = d
End Function

'--------------------------------------------------
' Fast lookup via dictionary (static cache)
'--------------------------------------------------
Private Function LookupRule(colName As String) As String
    Static dict As Object
    
    If dict Is Nothing Then Set dict = RuleDict
    
    If dict.Exists(colName) Then
        LookupRule = dict(colName)
    Else
        LookupRule = ""          ' default → copy unchanged
    End If
End Function


