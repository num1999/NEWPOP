var builder = WebApplication.CreateBuilder(args);

// --- [추가 1] 세션 서비스 등록 (builder.Build() 이전) ---
builder.Services.AddDistributedMemoryCache(); // 세션 데이터를 저장할 메모리 캐시
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 세션 만료 시간 (30분)
    options.Cookie.HttpOnly = true;                // 보안 설정
    options.Cookie.IsEssential = true;             // 필수 쿠키 설정
});

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.UseStaticFiles();
app.UseRouting();

// --- [추가 2] 세션 미들웨어 활성화 (반드시 Routing과 Authorization 사이에 위치!) ---
app.UseSession();

app.UseAuthorization();

app.Run();
//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.
//builder.Services.AddRazorPages();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}

//app.UseHttpsRedirection();

//app.UseRouting();

//app.UseAuthorization();

//app.MapStaticAssets();
//app.MapRazorPages()
//   .WithStaticAssets();

//app.Run();
