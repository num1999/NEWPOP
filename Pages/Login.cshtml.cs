using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace NEWPOP.Pages
{
    public class LoginModel : PageModel
    {
        public void OnGet()
        {
        }

        private readonly string _connStr;
        public LoginModel(IConfiguration config) => _connStr = config.GetConnectionString("OracleConn");

        [BindProperty]
        public LoginInput Input { get; set; }

        public class LoginInput
        {
            public string UserId { get; set; }
            public string Password { get; set; }
        }

        public async Task<IActionResult> OnPostLoginAsync()
        {
            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                // 실제 서비스에서는 암호화된 비밀번호를 비교해야 합니다.
                string sql = "SELECT COUNT(*) FROM USERS WHERE USER_ID = :id AND USER_PW = :pw";
                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("id", Input.UserId);
                cmd.Parameters.Add("pw", Input.Password);

                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count > 0) return RedirectToPage("/Main");
            }
            ModelState.AddModelError(string.Empty, "로그인 실패");
            return Page();
        }
    }
}
