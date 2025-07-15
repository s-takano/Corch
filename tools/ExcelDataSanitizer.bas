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
            ApplyRule = "����_" & Format(rowIdx, "00000")
        
        Case "mask_phone"
            ApplyRule = "090-" & Format(10000000 + Int(89999999 * Rnd), "00000000")
        
        Case "mask_post"
            ApplyRule = Format(1600000 + Int(9999 * Rnd), "000\-0000")
        
        Case "mask_email"
            ApplyRule = "user" & Format(rowIdx, "00000") & "@example.com"
        
        Case "shift_date"
            If IsDate(valIn) Then _
                ApplyRule = CDate(valIn) + Int(60 * Rnd) - 30   '�}30-day jitter
        
        '�\�\�\ NEW handlers �\�\�\
        Case "mask_id"
            ApplyRule = "ID" & Format(rowIdx, "00000")
        
        Case "mask_note"
            ApplyRule = "���l_" & Format(rowIdx, "00000")
        
        Case "mask_addr"
            ApplyRule = "�����s�T���v��" & Format(rowIdx, "00000")
        
        '�\�\�\ Pass-through options �\�\�\
        Case "leave", ""
            ApplyRule = valIn
        
        Case Else
            'safety fallback: copy unchanged
            ApplyRule = valIn
    End Select
End Function

'--------------------------------------------------
' Build once-per-run dictionary:  Header �� Rule
'--------------------------------------------------
Private Function RuleDict() As Object
    Dim d As Object
    Set d = CreateObject("Scripting.Dictionary")   ' late binding; no reference needed
    d.CompareMode = vbTextCompare                  ' case-insensitive keys
    
    '�\�\�\ Primary parties �\�\�\
    d.Add "�_���_��", "mask_name"
    d.Add "������1_��", "mask_name"
    d.Add "�A�ѕۏؐl1_��", "mask_name"
    d.Add "�ً}�A����_��", "mask_name"
    
    '�\�\�\ IDs / notes �\�\�\
    d.Add "�ۏ؉��ID", "mask_id"
    d.Add "���l1", "mask_note"
    d.Add "���l2", "mask_note"
    d.Add "�_���_�J�i", "mask_name"
    d.Add "�_��Җ�", "mask_name"
    
    '�\�\�\ Contract-holder contact �\�\�\
    d.Add "�_���_�X�֔ԍ�", "mask_post"
    d.Add "�_���_�Z��", "mask_addr"
    d.Add "�_���_�d�b�ԍ�", "mask_phone"
    d.Add "�_���_�g�ѓd�b�ԍ�", "mask_phone"
    d.Add "�_���_���[���A�h���X", "mask_email"
    d.Add "�_���_���N����", "shift_date"
    d.Add "�_���_�Ζ��於", "mask_name"
    d.Add "�_���_�Ζ���d�b�ԍ�", "mask_phone"
    
    '�\�\�\ Guarantor �\�\�\
    d.Add "�A�ѕۏؐl1_�X�֔ԍ�", "mask_post"
    d.Add "�A�ѕۏؐl1_�Z��", "mask_addr"
    d.Add "�A�ѕۏؐl1_�d�b�ԍ�", "mask_phone"
    d.Add "�A�ѕۏؐl1_���[���A�h���X", "mask_email"
    d.Add "�A�ѕۏؐl1_�Ζ��於", "mask_name"
    d.Add "�A�ѕۏؐl1_�Ζ���d�b�ԍ�", "mask_phone"
    
    '�\�\�\ Emergency contact �\�\�\
    d.Add "�ً}�A����_�X�֔ԍ�", "mask_post"
    d.Add "�ً}�A����_�Z��", "mask_addr"
    d.Add "�ً}�A����_�d�b�ԍ�", "mask_phone"
    
    d.Add "��i�񍐎�", "mask_name"
    d.Add "�]�o�_����", "mask_name"
    d.Add "���Z���쐬��", "mask_name"
    d.Add "���Z�����t��", "mask_name"
    
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
        LookupRule = ""          ' default �� copy unchanged
    End If
End Function


