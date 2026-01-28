using Microsoft.AspNetCore.Hosting; // IWebHostEnvironment 사용을 위해 필수
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Oracle.ManagedDataAccess.Client;
using System.Collections;    // ArrayList 사용 
using System.Collections.Generic;
using System.Data;
using System.IO; // <-- File, Path 사용을 위해 반드시 필요
using System.Linq;                // AsEnumerable() 및 LINQ 사용을 위해 필수

namespace NEWPOP.Pages
{
    public class SearchField
    {
        public string Title { get; set; }
        public string Type { get; set; } // TextBox, ComboBox 등
        public string GroupId { get; set; }
        public string DefaultValue { get; set; }
        public bool IsNotNull { get; set; }
        public string RelData { get; set; }
        public string ClientId => $"condi_{Title.GetHashCode()}"; // 고유 ID 생성

        // ComboBox 아이템을 저장할 리스트 (Value, Text)
        public List<SelectListItem> Options { get; set; } = new();
    }

    public class MainModel : PageModel
    {
        private readonly string _connStr; // 변수만 선언 (값 할당은 생성자에서)
        

        private readonly IWebHostEnvironment _env; // <-- 추가

        // 생성자: 모든 [cite_start]표식을 제거했습니다.
        public MainModel(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connStr = configuration.GetConnectionString("OracleConn");
            _env = env;
        }

        // 상위 메뉴 데이터를 담을 그릇
        public DataTable MainMenus { get; set; }
        public DataTable SubMenus { get; set; }
        public DataTable GridData { get; set; }
        public List<SearchField> DynamicFields { get; set; } = new();

        public string UserName { get; set; }
        public string UserRole { get; set; }
        public string UserID { get; set; }
        public string Lang { get; set; }


        public List<string> SubMenuList { get; set; } = new();
        //public DataTable GridData { get; set; }
            
        private void LoadXmlConfiguration(string menuId)
        {
            
            string filePath = Path.Combine(_env.WebRootPath, "config", $"{menuId}.xml");

            if (!System.IO.File.Exists(filePath)) return; // 

            DataSet ds = new DataSet();
            ds.ReadXml(filePath); // 

            if (ds.Tables.Contains("FIELD_INFO"))
            {
                foreach (DataRow row in ds.Tables["FIELD_INFO"].Rows)
                {
                    DynamicFields.Add(new SearchField
                    {
                        Title = row["FIELD_TITLE"].ToString(),
                        Type = row["FIELD_TYPE"].ToString(),
                        GroupId = row["FIELD_GROUP_ID"].ToString(),
                        IsNotNull = row["NOTNULL"].ToString().ToLower() == "true"
                    });
                }
            }
        }

        public async Task<IActionResult> OnGetAsync(string topMenuId, string selectedMenuId)
        {
            // 1. 세션 정보 가져오기
            UserID = HttpContext.Session.GetString("UserID");
            Lang = HttpContext.Session.GetString("Lang");
            if (string.IsNullOrEmpty(UserID)) return RedirectToPage("/Index");

            // 2. 상단 메뉴는 항상 로드 (9개 버튼)
            await LoadMenuGroup(UserID);

            // 3. 상단 메뉴를 클릭했다면 서브 메뉴 로드
            if (!string.IsNullOrEmpty(topMenuId))
            {
                await LoadSubMenus(topMenuId);
            }
            // [중요] 4. 서브메뉴(상세페이지)가 선택되었다면 XML 설정 로드
            if (!string.IsNullOrEmpty(selectedMenuId))
            {
                //LoadXmlConfiguration(selectedMenuId);
                LoadXmlConfigurationAsync(selectedMenuId);
            }
            return Page();
        }

        private async Task LoadMenuGroup(string userId)
        {
            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (OracleCommand cmd = new OracleCommand("PK_WEB.sp_menu_group", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 1. 입력 파라미터
                    cmd.Parameters.Add("p_user_id", OracleDbType.Varchar2).Value = userId;

                    // 2. 출력 파라미터 (커서)
                    OracleParameter refCursor = new OracleParameter("p_return_rec", OracleDbType.RefCursor);
                    refCursor.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(refCursor);

                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        MainMenus = new DataTable();
                        da.Fill(MainMenus);
                    }
                }
            }
        }

        private async Task LoadSubMenus(string topMenuId)
        {
            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (OracleCommand cmd = new OracleCommand("PK_MENU.SP_MENU_SELECT3", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 프로시저 파라미터 설정 (순서와 타입 주의)
                    cmd.Parameters.Add("p_level", OracleDbType.Int32).Value = 1; // 보통 서브메뉴는 레벨 2
                    cmd.Parameters.Add("p_parent_id", OracleDbType.Varchar2).Value = "1";
                    cmd.Parameters.Add("p_group_id", OracleDbType.Varchar2).Value = topMenuId; // 필요시 설정
                    cmd.Parameters.Add("p_parent_title", OracleDbType.Varchar2).Value = ""; // 필요시 설정
                    cmd.Parameters.Add("p_user_id", OracleDbType.Varchar2).Value = UserID;
                    cmd.Parameters.Add("P_LANG_ID", OracleDbType.Varchar2).Value = Lang;

                    OracleParameter curOut = new OracleParameter("CUR_OUT", OracleDbType.RefCursor);
                    curOut.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(curOut);

                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        SubMenus = new DataTable();
                        da.Fill(SubMenus);
                    }
                }
            }
        }

        // Razor 화면에서 부모 ID로 자식 노드들을 필터링하기 위한 헬퍼 함수
        public EnumerableRowCollection<DataRow> GetChildMenus(string parentId)
        {
            return SubMenus.AsEnumerable()
                           .Where(r => r["PARENT"].ToString() == parentId);
        }

        // 결과를 담을 프로퍼티

        public async Task<IActionResult> OnPostSearchAsync(string selectedMenuId)
        {
            if (string.IsNullOrEmpty(selectedMenuId)) return Page();

            // 1. XML에서 LAYOUT_INFO 읽기
            string xmlPath = Path.Combine(_env.WebRootPath, "config", $"{selectedMenuId}.xml");
            if (!System.IO.File.Exists(xmlPath)) return Page();

            DataSet configDs = new DataSet();
            configDs.ReadXml(xmlPath);

            // 2. 전체 컨트롤의 입력값 순서대로 수집 (0부터 시작하는 인덱스 기준)
            var fieldRows = configDs.Tables["FIELD_INFO"].Rows;
            List<string> allValues = new List<string>();
            foreach (DataRow field in fieldRows)
            {
                string fieldTitle = field["FIELD_TITLE"].ToString();
                string val = Request.Form[fieldTitle];
                allValues.Add(string.IsNullOrEmpty(val) ? "" : val);
            }

            // 3. P_PARAM 구성을 위한 전체 세미콜론 연결값
            string combinedParams = string.Join(";", allValues);

            // 4. FIELD_GROUP_REL_Data 파싱 (p_where 파라미터용)
            // 예: "PLANT_ID = '[0]'" -> "PLANT_ID = '1000'"
            string relData = configDs.Tables["FIELD_INFO"].Rows[0]["FIELD_GROUP_REL_Data"]?.ToString() ?? "";
            

            // 5. 프로시저 호출 (추가된 pWhere 전달)
            DataRow layoutRow = configDs.Tables["LAYOUT_INFO"].Rows[0];
            string procedureName = layoutRow["SID"].ToString().Split('@').Last();

            await FetchGridData(procedureName, combinedParams);

            LoadXmlConfiguration(selectedMenuId);
            return Page();
            // LAYOUT_INFO 데이터 추출 [cite: 2]
            //DataRow layoutRow = configDs.Tables["LAYOUT_INFO"].Rows[0];
            //string rawSid = layoutRow["SID"].ToString();
            //string procedureName = rawSid.Contains("@") ? rawSid.Split('@')[1] : rawSid; 

            //// 검색 조건 구성 [cite: 2]
            //List<string> paramList = new List<string>();
            //foreach (DataRow field in configDs.Tables["FIELD_INFO"].Rows)
            //{
            //    string fieldTitle = field["FIELD_TITLE"].ToString();
            //    string val = Request.Form[fieldTitle];
            //    paramList.Add(string.IsNullOrEmpty(val) ? "" : val);
            //}
            //string combinedParams = string.Join(";", paramList); 

            //await FetchGridData(procedureName, combinedParams);
            //LoadXmlConfiguration(selectedMenuId);

            //return Page();
        }

        // [숫자]를 실제 값으로 치환하는 헬퍼 함수
        private string ParseRelData(string relData, List<string> values)
        {
            if (string.IsNullOrEmpty(relData)) return "";

            for (int i = 0; i < values.Count; i++)
            {
                string target = $"[{i}]";
                if (relData.Contains(target))
                {
                    relData = relData.Replace(target, values[i]);
                }
            }
            return relData;
        }

        private async Task FetchGridData(string spName, string paramString)
        {
            UserID = HttpContext.Session.GetString("UserID"); // 세션에서 유저 ID 다시 확보

            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (OracleCommand cmd = new OracleCommand(spName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("P_PARAM", OracleDbType.Varchar2).Value = paramString;
                    cmd.Parameters.Add("P_USER_ID", OracleDbType.Varchar2).Value = UserID;

                    OracleParameter refCursor = new OracleParameter("CUR_OUT", OracleDbType.RefCursor);
                    refCursor.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(refCursor);

                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        GridData = new DataTable();
                        da.Fill(GridData);
                    }
                }
            }
        }

        private async Task LoadXmlConfigurationAsync(string menuId)
        {
            DynamicFields.Clear();
            string filePath = Path.Combine(_env.WebRootPath, "config", $"{menuId}.xml");
            if (!System.IO.File.Exists(filePath)) return;

            DataSet ds = new DataSet();
            ds.ReadXml(filePath);

            if (ds.Tables.Contains("FIELD_INFO"))
            {
                foreach (DataRow row in ds.Tables["FIELD_INFO"].Rows)
                {
                    var field = new SearchField
                    {
                        Title = row["FIELD_TITLE"]?.ToString(),
                        Type = row["FIELD_TYPE"]?.ToString(),
                        GroupId = row["FIELD_GROUP_ID"]?.ToString(),
                        IsNotNull = row["NOTNULL"]?.ToString().ToLower() == "true",
                        RelData = row["FIELD_GROUP_REL_Data"]?.ToString(),
                        DefaultValue = row["FIELD_URL"]?.ToString()
                    };

                    // ComboBox이고 GroupID가 있는 경우 DB 조회
                    if (field.Type == "RadioButton" && !string.IsNullOrEmpty(field.GroupId))
                    {
                        string[] items = field.GroupId.Split(',');
                        foreach (var item in items)
                        {
                            // "정상[1]" 형태에서 텍스트와 값을 분리
                            int sPos = item.IndexOf('[');
                            int ePos = item.IndexOf(']');
                            if (sPos > -1 && ePos > -1)
                            {
                                field.Options.Add(new SelectListItem
                                {
                                    Text = item.Substring(0, sPos),
                                    Value = item.Substring(sPos + 1, ePos - sPos - 1)
                                });
                            }
                        }
                    }
                    if (field.Type == "ComboBox" && !string.IsNullOrEmpty(field.GroupId))
                    {
                        // [분기 처리] 쉼표와 대괄호가 있으면 직접 파싱 (Radio 버튼과 동일 로직)
                        if (field.GroupId.Contains("[") && field.GroupId.Contains(","))
                        {
                            string[] items = field.GroupId.Split(',');
                            foreach (var item in items)
                            {
                                int sPos = item.IndexOf('[');
                                int ePos = item.IndexOf(']');
                                if (sPos > -1 && ePos > -1)
                                {
                                    field.Options.Add(new SelectListItem
                                    {
                                        Text = item.Substring(0, sPos),
                                        Value = item.Substring(sPos + 1, ePos - sPos - 1)
                                    });
                                }
                            }
                        }
                        // 그렇지 않으면 기존처럼 DB 프로시저(sp_code_list) 호출 
                    else
                        {
                            field.Options = await GetCodeListAsync(field.GroupId, field.RelData);
                        }
                    }
                    else if (field.Type == "Date")
                    {
                        if (string.IsNullOrEmpty(field.DefaultValue))
                            field.DefaultValue = DateTime.Now.ToString("yyyy-MM-dd");
                    }
                    else if (field.Type == "DateTime")
                    {
                        if (string.IsNullOrEmpty(field.DefaultValue))
                        {
                            // HTML5 datetime-local 형식은 'yyyy-MM-ddTHH:mm' (T가 구분자)
                            field.DefaultValue = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
                        }
                    }
                    DynamicFields.Add(field);
                }
            }
        }

        private async Task<List<SelectListItem>> GetCodeListAsync(string groupId, string relData)
        {
            var list = new List<SelectListItem>();
            // 1. [숫자] 형태의 인덱스를 현재 DynamicFields에 담긴 값으로 치환
            // 콤보박스 로딩 시점에는 이전 컨트롤들의 값(DefaultValue 또는 입력값)을 참조합니다.
            string pWhere = relData ?? "";
            for (int i = 0; i < DynamicFields.Count; i++)
            {
                string target = $"[{i}]";
                if (pWhere.Contains(target))
                {
                    // 앞서 생성된 컨트롤의 현재 값을 가져와 치환 (DefaultValue 혹은 현재 입력값)
                    pWhere = pWhere.Replace(target, DynamicFields[i].DefaultValue ?? "");
                }
            }

            using (OracleConnection conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (OracleCommand cmd = new OracleCommand("PK_WEB.sp_code_id", conn)) // 프로시저명 확인 필요 (PK_WEB.sp_code_list 등)
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_group_id", OracleDbType.Varchar2).Value = groupId;
                    cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value = pWhere;

                    OracleParameter refCursor = new OracleParameter("p_return_rec", OracleDbType.RefCursor);
                    refCursor.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(refCursor);

                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        foreach (DataRow row in dt.Rows)
                        {
                            // 프로시저 반환 컬럼명(CODE, NAME 등)에 맞춰 수정하세요
                            list.Add(new SelectListItem
                            {
                                Value = row[0].ToString(), // 코드값
                                Text = row[1].ToString()   // 표시명칭
                            });
                        }
                    }
                }
            }
            return list;
        }
    }
}