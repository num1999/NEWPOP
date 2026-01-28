using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client; // Oracle 라이브러리
using System.Data;
using Microsoft.AspNetCore.Mvc.Rendering; // SelectListItem 사용을 위해 필요

public class IndexModel : PageModel
{
    private readonly string _connStr; // 변수만 선언 (값 할당은 생성자에서)

    // 1. 생성자를 통해 IConfiguration(설정값 관리자)을 받습니다.
    public IndexModel(IConfiguration configuration)
    {
        // 2. appsettings.json의 "ConnectionStrings" 섹션에서 "OracleConn" 값을 읽어옵니다.
        _connStr = configuration.GetConnectionString("OracleConn");
    }

    // 화면의 콤보박스와 연결될 리스트
    public List<SelectListItem> LanguageOptions { get; set; } = new();

    // [중요] HTML의 asp-for와 연결되는 프로퍼티입니다.
    [BindProperty]
    public string LoginId { get; set; }

    [BindProperty]
    public string LoginPw { get; set; }

    [BindProperty]
    public string SelectedLanguage { get; set; }

    [BindProperty]
    public string ClientIp { get; set; } // IP 주소
    public async Task OnGetAsync()
    {
        // IP 정보 가져오기 (기존 로직)
        ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (ClientIp == "::1") ClientIp = "127.0.0.1";

        // DB에서 언어 코드 가져오기
        await LoadLanguageCodes();
    }

    private async Task LoadLanguageCodes()
    {
        using (OracleConnection conn = new OracleConnection(_connStr))
        {
            await conn.OpenAsync();
            using (OracleCommand cmd = new OracleCommand("PK_ATM.sp_language_CODE", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // 1. 입력 파라미터 (P_KOR) - 예: "Y" 또는 빈값
                cmd.Parameters.Add("P_KOR", OracleDbType.Varchar2).Value = "Y";

                // 2. 출력 파라미터 (P_RETURN_REC 커서)
                OracleParameter refCursor = new OracleParameter("P_RETURN_REC", OracleDbType.RefCursor);
                refCursor.Direction = ParameterDirection.Output;
                cmd.Parameters.Add(refCursor);

                using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    // 3. DataTable 데이터를 SelectListItem 리스트로 변환
                    foreach (DataRow row in dt.Rows)
                    {
                        LanguageOptions.Add(new SelectListItem
                        {
                            Value = row["@@Lang_id"].ToString(),
                            Text = row["@@Lang_name"].ToString()
                        });
                    }
                }
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLanguageCodes();
            return Page();
        }

        try
        {
            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (OracleCommand cmd = new OracleCommand("PK_ATM.SP_LOGIN", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 파라미터 설정 (기존과 동일)
                    cmd.Parameters.Add("p_id", OracleDbType.Varchar2).Value = LoginId;
                    cmd.Parameters.Add("p_pw", OracleDbType.Varchar2).Value = LoginPw;
                    cmd.Parameters.Add("p_ip", OracleDbType.Varchar2).Value = ClientIp;
                    cmd.Parameters.Add("p_lang_id", OracleDbType.Varchar2).Value = SelectedLanguage;

                    OracleParameter refCursor = new OracleParameter("p_return_rec", OracleDbType.RefCursor);
                    refCursor.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(refCursor);

                    DataTable dt = new DataTable();
                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        // [핵심] DB에서 raise_application_error가 발생하면 여기서 Exception이 던져집니다.
                        da.Fill(dt);
                    }

                    if (dt.Rows.Count > 0)
                    {
                        DataRow userRow = dt.Rows[0];
                        HttpContext.Session.SetString("UserID", userRow["ID"].ToString());
                        HttpContext.Session.SetString("UserName", userRow["NAME"].ToString());
                        HttpContext.Session.SetString("UserRole", userRow["AUTH"].ToString());
                        HttpContext.Session.SetString("Lang", SelectedLanguage);

                        return RedirectToPage("/Main");
                    }
                }
            }
        }
        catch (OracleException ex)
        {
            // Oracle에서 보낸 에러 메시지를 가공하여 화면에 표시
            // ex.Number가 20000인 경우 등을 체크할 수 있습니다.
            if (ex.Number >= 20000 && ex.Number <= 20999)
            {
                // "ORA-20000: 메세지..." 형태에서 메세지 부분만 추출하거나 그대로 보여줍니다.
                string customMessage = ex.Message.Split('\n')[0]; // 첫 줄만 추출
                ModelState.AddModelError(string.Empty, customMessage);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "데이터베이스 연결 오류가 발생했습니다.");
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "시스템 오류: " + ex.Message);
        }

        // 에러 발생 시 콤보박스 목록을 다시 채워줘야 화면이 깨지지 않습니다.
        await LoadLanguageCodes();
        return Page();
    }

}

