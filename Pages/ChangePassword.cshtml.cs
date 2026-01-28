using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;

namespace NEWPOP.Pages
{
    public class ChangePasswordModel : PageModel
    {
        private readonly string _connStr;

        public ChangePasswordModel(IConfiguration config)
        {
            _connStr = config.GetConnectionString("OracleConn");
        }

        // 이 부분이 누락되어 오류가 발생한 것입니다!
        [BindProperty]
        public PasswordUpdateInput UpdateData { get; set; }

        // 입력 데이터를 담을 내부 클래스 정의
        public class PasswordUpdateInput
        {
            public string UserId { get; set; }
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
        }

        public void OnGet()
        {
            // 페이지 로드 시 실행
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                // 기존 아이디와 비밀번호가 일치할 때만 새 비밀번호로 업데이트
                string sql = "UPDATE USERS SET USER_PW = :newPw WHERE USER_ID = :id AND USER_PW = :oldPw";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("newPw", UpdateData.NewPassword);
                cmd.Parameters.Add("id", UpdateData.UserId);
                cmd.Parameters.Add("oldPw", UpdateData.OldPassword);

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    // 변경 성공 시 로그인 페이지로 이동
                    return RedirectToPage("/Login");
                }
                else
                {
                    // 변경 실패 (아이디나 기존 비번 불일치)
                    ModelState.AddModelError(string.Empty, "아이디 또는 기존 비밀번호가 틀립니다.");
                    return Page();
                }
            }
        }
    }
}
