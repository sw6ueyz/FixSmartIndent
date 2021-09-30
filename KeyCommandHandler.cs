using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;


// 지정된 SnapshotPoint 가 포함된 행으로부터 역순으로 모든 글자를 읽는 도우미
// 한 글자를 미리 엿볼 수 있다.
internal class BackwardReader
{
    ITextSnapshotLine m_line;
    string m_sLine;
    int m_nCol;
    char m_cPeek;

    // lastPoint 직전의 문자부터 역순으로 읽어 들인다.
    public BackwardReader( SnapshotPoint lastPoint )
    {
        m_line = lastPoint.GetContainingLine();
        m_sLine = m_line.GetText();
        m_nCol = lastPoint - m_line.Start - 1;
        PreparePeek();
    }

    // '\0' 은 BOF 를 의미한다.
    public char Read()
    {
        char cPeek = m_cPeek;
        PreparePeek();
        return cPeek;
    }

    // '\0' 은 BOF 를 의미한다.
    public char Peek()
    {
        return m_cPeek;
    }

    private void PreparePeek()
    {
        if ( 0 <= m_nCol ) {
            m_cPeek = m_sLine[ m_nCol-- ];
            return;
        }
        int n = m_line.LineNumber;
        if ( 0 < n ) {
            m_line = m_line.Snapshot.GetLineFromLineNumber( n - 1 );
            m_sLine = m_line.GetText();
            m_nCol = m_sLine.Length - 1;
            m_cPeek = '\n';
            return;
        }
        m_cPeek = '\0';
    }
}

internal static class Util
{
    // 탭과 공백 문자를 모두 넘긴다.
    public static void SkipSpaceTab( BackwardReader br )
    {
        for ( char c; ( c = br.Peek() ) == ' ' || c == '\t'; )
            br.Read();
    }

    // 현재 문자열을 넘긴다. cOpener 는 문자열을 연 문자.
    // 문자열이 열린 경우 true
    public static bool SkipString( BackwardReader br, char cOpener )
    {
        char c;
        for ( ; ; ) {
            
            c = br.Read();

            if ( c == '\0' )
                return false;

            // 현재 역순으로 문자열을 읽고 있음에 유의할 것.
            // 열기 문자열 다음에 '\\' 문자가 나온 경우
            // 그 '\\' 문자가 또 다시 escape 되어 있는가를 검사할 필요가 없이
            // 일단 그 문자도 문자열 내부인 것이 확실하다.
            if ( c == cOpener && br.Peek() != '\\' )
                return true;
        }
    }

    // 현재 cCloser 로 괄호가 열린 상태로 가정하고 이 괄호 전체를 넘긴다.
    public static bool SkipParenthesis( BackwardReader br
        , char cOpener, char cCloser )
    {
        int nLevel = 0;
        char c;
        for ( ; ; ) {

            c = br.Read();

            if ( c == cOpener ) {
                if ( --nLevel < 0 )
                    return true;
            }
            else if ( c == cCloser )
                ++nLevel;
            else if ( c == '\0' )
                return false;
            else if ( c == '\'' || c == '`' || c == '"' )
                SkipString( br, c );
        }
    }

    // 행 끝에 '{' 문자가 등장할 때까지 넘긴다.
    // 그런 문자를 읽은 경우 true
    public static bool SkipToDanglingOpenBracket( BackwardReader br )
    {
        int nLevel = 0;
        char c;
        for ( ; ; ) switch ( c = br.Read() ) {

            case '\n' :
                // '{' 뒤의 공백 문자가 있을 수 있으므로 제거 후 검사한다.
                SkipSpaceTab( br );
                if ( nLevel == 0 && br.Peek() == '{' ) {
                    br.Read();
                    return true;
                }
                break;

            case '}' :
                ++nLevel;
                break;

            case '{' :
                if ( --nLevel < 0 )
                    return false;
                break;

            case '"' :
            case '\'' :
            case '`' :
                SkipString( br, c );
                break;

            case '\0' :
                return false;

            default :
                break;
        }
    }

    // 현재 행의 인덴트를 얻는다. 현재 위치부터 측정한다.
    public static string GetIndent( BackwardReader br )
    {
        var instance = PooledStringBuilder.GetInstance();
        try {
            var sb = instance.Builder;
            char c;
            for ( ; ; ) switch ( c = br.Read() ) {

                case '\n' :
                case '\0' :
                    return sb.ToString();

                case '\t' :
                case ' ' :
                    // 역순으로 전달되므로 c 를 앞쪽에 삽입한다.
                    sb.Insert( 0, c );
                    break;

                case '"' :
                case '\'' :
                case '`' :
                    sb.Clear();
                    SkipString( br, c );
                    break;

                case ')' :
                    sb.Clear();
                    SkipParenthesis( br, '(', ')' );
                    break;

                default :
                    sb.Clear();
                    break;
            }
        }
        finally {
            instance.Free();
        }
    }
}


[Export(typeof(ICommandHandler))]
[ContentType("text")]
[Name("FixSmartIndent_Brace")]
internal class BraceKeyCommandHandler : ICommandHandler<TypeCharCommandArgs>
{
    public string DisplayName => "FixSmartIndent(Brace)";

    public BraceKeyCommandHandler()
	{
	}

    public CommandState GetCommandState(TypeCharCommandArgs args)
    {
        return CommandState.Unspecified;
    }

    public bool ExecuteCommand(
        TypeCharCommandArgs args, CommandExecutionContext executionContext)
    {
        // '{'/'}' 키를 눌렀을 때만 처리
        char cTypedChar = args.TypedChar;
        if ( cTypedChar != '{' && cTypedChar != '}' )
            return false;

        // 선택이 있는 경우 무시
        var tv = args.TextView;
        var sel = tv.Selection;
        if ( !sel.IsEmpty )
            return false;

        // 현재 행이 비어 있는 경우에만 처리
        var apos = tv.Caret.Position.BufferPosition;
        var line = apos.GetContainingLine();
        if ( line.LineNumber == 0 )
            return false;
        if ( 0 < line.GetText().Trim().Length )
            return false;

        BackwardReader br;

        if ( cTypedChar == '}' ) {
            // 행 끝에 '{' 가 있는 위치로 이동
            br = new BackwardReader( apos );
            if ( !Util.SkipToDanglingOpenBracket( br ) )
                return false;
        }
        else {
            // cTypedChar == '{'
            br = new BackwardReader(
                apos.Snapshot.GetLineFromLineNumber( line.LineNumber - 1 ).End );
        }

        // 그 행의 인덴트를 측정
        var sIndent = Util.GetIndent( br );
        if ( sIndent == null )
            return false;

        // 키를 누른 행을 인덴트와 '{'/'}' 문자를 결합하여 입력
        int nLine = line.LineNumber;
        args.SubjectBuffer.Replace( line.Extent, sIndent + cTypedChar );

        // 캐럿을 '{'/'}' 의 뒤로 이동
        // 이 때 스냅샷이 변경되었으므로
        // 위의 로컬 변수들을 무시하고 다시 읽어야 한다.
        tv.Caret.MoveTo(
            tv.TextSnapshot.GetLineFromLineNumber( nLine ).Start
                + ( sIndent.Length + 1 ) );

        // 처리 되었으므로 더 이상의 진행을 방지
        return true;
    }
}


[Export(typeof(ICommandHandler))]
[ContentType("text")]
[Name("FixSmartIndent_Return")]
internal class ReturnKeyCommandHandler : ICommandHandler<ReturnKeyCommandArgs>
{
    public string DisplayName => "FixSmartIndent(Return)";

    public CommandState GetCommandState(ReturnKeyCommandArgs args)
    {
        return CommandState.Unspecified;
    }

    public bool ExecuteCommand(
        ReturnKeyCommandArgs args, CommandExecutionContext executionContext)
    {
        // 선택이 있는 경우 무시
        var tv = args.TextView;
        var sel = tv.Selection;
        if ( !sel.IsEmpty )
            return false;

        // 리턴 키를 누른 위치에서 그 후의 문자열이 '{'/'}' 뿐인 경우에만 처리
        var apos = tv.Caret.Position.BufferPosition;
        var line = apos.GetContainingLine();
        var sLine = line.GetText();
        var nHead = apos - line.Start;
        var sTrail = sLine.Substring( nHead ).Trim();
        if ( sTrail.Length != 1 )
            return false;
        char cTrail = sTrail[ 0 ];
        if ( cTrail != '{' && cTrail != '}' )
            return false;

        var br = new BackwardReader( apos );

        if ( cTrail == '}' ) {
            // 행 끝에 '{' 가 있는 위치로 이동
            if ( !Util.SkipToDanglingOpenBracket( br ) )
                return false;
        }

        // 그 행의 인덴트를 측정
        var sIndent = Util.GetIndent( br );
        if ( sIndent == null )
            return false;

        // 개행 문자열을 얻는다.
        int nLine = line.LineNumber;
        string sLineBreak;
        if ( 0 < line.LineBreakLength )
            sLineBreak = line.GetLineBreakText();
        else if ( 0 < nLine )
            sLineBreak =
                line.Snapshot.GetLineFromLineNumber( nLine - 1 )
                    .GetLineBreakText();
        else
            // 유일한 행이라서 알 수 없는 경우
            sLineBreak = "\r\n";

        // '{'/'}' 키를 누른 행을 두 행으로 나누어
        // 원래 행의 내용과 인덴트와 '{'/'}' 문자를 결합하여 입력
        args.SubjectBuffer.Replace( line.Extent
            , sLine.Substring( 0, nHead ) + sLineBreak + sIndent + cTrail );

        // 캐럿을 '{'/'}' 직전으로 이동 (키를 눌렀을 때와는 다름)
        // 이 때 스냅샷이 변경되었으므로 위의 로컬 변수들을 무시하고 다시 읽어야 한다.
        tv.Caret.MoveTo(
            tv.TextSnapshot.GetLineFromLineNumber( nLine + 1 ).Start
                + sIndent.Length );

        // 처리 되었으므로 더 이상의 진행을 방지
        return true;
    }
}
