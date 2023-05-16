using IGDB;
using IGDB.Models;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using noob_game.API;
using noob_game.API.Hubs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup =>
{
    // Include 'SecurityScheme' to use JWT Authentication
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        BearerFormat = "JWT",
        Name = "JWT Authentication",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        Description = "JWT Bearer Token:",

        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    setup.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    setup.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
                      builder =>
                      {
                          builder.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                      });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey
        (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}


app.UseHttpsRedirection();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();


var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);

// API Credits

var igdb = new IGDBClient(
  builder.Configuration["IGDB:ClientId"],
  builder.Configuration["IGDB:ClientSecret"]
);


var connStr = builder.Configuration["DB:Connection"];
IMongoClient mongoClient = new MongoClient(connStr);

var connDb = builder.Configuration["DB:DBName"];
IMongoDatabase db = mongoClient.GetDatabase(connDb);

app.MapHub<NoobGameHub>("/NoobGameHub");

// API Links
#region IGDB
app.MapGet("/topGames", async () =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields id,name, artworks.*, first_release_date; where artworks.width>=1920 & artworks.height>=1080 & first_release_date>={Helpers.UTSOneMonthAgo()} & first_release_date<={Helpers.UTSNow()} & first_release_date!=null & total_rating!=null; sort total_rating desc; limit 10;");
    return Results.Ok(games);
});

app.MapGet("/top20GamesAllTimes", async () =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields id,name, cover.image_id, total_rating, total_rating_count,  genres; where first_release_date<={Helpers.UTSNow()} & first_release_date!=null & total_rating>0 & cover.image_id!=null & total_rating_count > 100; sort total_rating desc; limit 20;");
    var genres = await igdb.QueryAsync<Genre>(IGDBClient.Endpoints.Genres, query: "fields checksum,created_at,name,slug,updated_at,url;limit 100;");

    List<HypesOfYear> top20GamesAllTimes = new List<HypesOfYear>();

    var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
    await Parallel.ForEachAsync(games, options, async (game, token) =>
    {
        HypesOfYear top20Game = new HypesOfYear();
        top20Game.Id = game.Id;
        top20Game.Name = game.Name;
        top20Game.TotalRating = game.TotalRating;
        top20Game.Cover = game.Cover.Value.ImageId;

        var gameGenres = game.Genres.Ids.ToList();

        var selectedGenres = genres.Where(x => gameGenres.Contains((long)x.Id)).Select(x => x.Name).ToList();
        top20Game.Genre = string.Join(", ", selectedGenres);
        top20GamesAllTimes.Add(top20Game);
    });

    return Results.Ok(top20GamesAllTimes);
});

app.MapGet("/popularGames", async () =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields id, name, total_rating, genres;where first_release_date>={Helpers.UTSOneMonthAgo()} & first_release_date<={Helpers.UTSNow()} & first_release_date!=null & total_rating>0;sort first_release_date desc; sort total_rating desc;limit 6;");
    var genres = await igdb.QueryAsync<Genre>(IGDBClient.Endpoints.Genres, query: "fields checksum,created_at,name,slug,updated_at,url;limit 100;");

    var gameIds = string.Join(",", games.Select(x => x.Id));

    var screenShoots = await igdb.QueryAsync<Screenshot>(IGDBClient.Endpoints.Screenshots, query: $"fields image_id, game; where game=({gameIds}); limit 100;");

    List<PopularGame> popularGames = new List<PopularGame>();

    var options = new ParallelOptions { MaxDegreeOfParallelism = 6 };
    await Parallel.ForEachAsync(games, options, async (game, token) =>
    {
        PopularGame popularGame = new PopularGame();
        popularGame.Id = game.Id;
        popularGame.Name = game.Name;
        popularGame.TotalRating = game.TotalRating;

        var gameGenres = game.Genres.Ids.ToList();

        var selectedGenres = genres.Where(x => gameGenres.Contains((long)x.Id)).Select(x => x.Name).ToList();
        popularGame.Genre = string.Join(", ", selectedGenres);
        popularGame.ImageList = screenShoots.Where(x => x.Game.Id == game.Id).Select(x => x.ImageId).ToList();

        popularGames.Add(popularGame);
    });

    return Results.Ok(popularGames);
});

// 6 - PC
// 167 - PS5
// 169 - XBox
app.MapGet("/lastPlatformGames", async (int id) =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields id,name, cover.image_id, first_release_date;where first_release_date>={Helpers.UTSNow()} & first_release_date!=null & cover.image_id!=null & platforms = [{id}]; sort first_release_date; limit 5;");
    return Results.Ok(games);
});

app.MapGet("/hypesOfYear", async () =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields id,name, cover.image_id, total_rating, genres;where first_release_date>={Helpers.UTSBeginningOfYear()} & first_release_date<={Helpers.UTSNow()} & first_release_date!=null & cover.image_id!=null & hypes>0; sort hypes desc; limit 10;");
    var genres = await igdb.QueryAsync<Genre>(IGDBClient.Endpoints.Genres, query: "fields checksum,created_at,name,slug,updated_at,url;limit 100;");

    List<HypesOfYear> hypes = new List<HypesOfYear>();

    var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
    await Parallel.ForEachAsync(games, options, async (game, token) =>
    {
        HypesOfYear hype = new HypesOfYear();
        hype.Id = game.Id;
        hype.Name = game.Name;
        hype.TotalRating = game.TotalRating;
        hype.Cover = game.Cover.Value.ImageId;

        var gameGenres = game.Genres.Ids.ToList();

        var selectedGenres = genres.Where(x => gameGenres.Contains((long)x.Id)).Select(x => x.Name).ToList();
        hype.Genre = string.Join(", ", selectedGenres);
        hypes.Add(hype);
    });


    return Results.Ok(hypes);
});

app.MapGet("/gameList", async () =>
{
    var games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: "fields id,name,genres, cover.*, artworks.image_id;");
    return Results.Ok(games);
});

app.MapGet("/gameDetail", async (int id) =>
{
    var game = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query: $"fields alternative_names.comment,alternative_names.name,cover.image_id,created_at,first_release_date,game_engines.name,game_modes.name,genres.name,hypes,involved_companies.company.name,name,platforms.name,platforms.platform_logo.image_id,player_perspectives.name,release_dates.human,release_dates.platform.name,screenshots.image_id,screenshots.height,screenshots.width,storyline,summary,total_rating,updated_at,videos.name,videos.video_id; where id = {id};");
    return Results.Ok(game[0]);
});
#endregion

#region USER
app.MapPost("/createToken", async (UserLogin user) =>
{
    var collection = db.GetCollection<User>("User");
    var xxx = collection.Find(x=> x.Name!="1").ToList();
    var emailCheck = collection.Find(x => x.Email == user.Email).FirstOrDefault();

    if (emailCheck == null)
        return Results.NotFound("E-Mail address is not found!");
    else if (emailCheck.Password != Helpers.ComputeSha256Hash(user.Password))
        return Results.NotFound("Password is incorrect!");
    else if (!emailCheck.Status)
        return Results.NotFound("User is not active. Check your e-mail!");
    else
    {

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
             }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials
            (new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha512Signature)
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = tokenHandler.WriteToken(token);

        return Results.Ok(new NewToken() { UserDetail = emailCheck.Adapt<UserDetail>(), Token = jwtToken, ExpireOn = token.ValidTo });
    }
});

app.MapPost("/addUser", async (User user) =>
{
    var result = new Result();
    var collection = db.GetCollection<User>("User");
    var emailCheck = collection.Find(x => x.Email == user.Email).FirstOrDefault();

    if (emailCheck == null)
    {
        // after mail 
        user.Status = true;

        user.Password = Helpers.ComputeSha256Hash(user.Password);
        collection.InsertOne(user);
        result.Status = true;
        result.Message = "User Added.";
    }
    else result.Message = "E-Mail address already exist!";

    return Results.Ok(result);
});

app.MapPost("/addFavourite", async (HttpRequest request, FavouriteDTO favouriteDTO) =>
{
    var result = new Result();
    var email = TokenParse(request);

    if (email == "")
    {
        result.Message = "Token expired!";
        return Results.Ok(result);
    }

    var colUser = db.GetCollection<User>("User");
    var userCheck = colUser.Find(x => x.Email == email).FirstOrDefault();

    if (userCheck == null)
    {
        result.Message = "User not found!";
        return Results.Ok(result);
    }

    var colFav = db.GetCollection<Favourite>("Favourite");
    var favCheck = colFav.Find(x => x.GameId == favouriteDTO.GameId && x.UserId == userCheck.Id).FirstOrDefault();

    if (favCheck == null)
    {
        var favourite = new Favourite() { UserId = userCheck.Id, GameId = favouriteDTO.GameId };
        colFav.InsertOne(favourite);
        result.Status = true;
        result.Message = "The game has been added to favourites.";
    }
    else result.Message = "The game already exist!";

    return Results.Ok(result);
}).RequireAuthorization();

app.MapDelete("/deleteFavourite", async (HttpRequest request, int gameId) =>
{
    var result = new Result();
    var email = TokenParse(request);

    if (email == "")
    {
        result.Message = "Token expired!";
        return Results.Ok(result);
    }

    var colUser = db.GetCollection<User>("User");
    var userCheck = colUser.Find(x => x.Email == email).FirstOrDefault();

    if (userCheck == null)
    {
        result.Message = "User not found!";
        return Results.Ok(result);
    }

    var colFav = db.GetCollection<Favourite>("Favourite");
    colFav.DeleteOne(x => x.GameId == gameId && x.UserId == userCheck.Id);

    result.Status = true;
    result.Message = "The game has been deleted to favourites.";

    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/userFavourites", async (HttpRequest request) =>
{
    var email = TokenParse(request);

    if (email == "")
    {
        return Results.Ok(new List<int>());
    }

    var colUser = db.GetCollection<User>("User");
    var userCheck = colUser.Find(x => x.Email == email).FirstOrDefault();

    if (userCheck == null)
    {
        return Results.Ok(new List<int>());
    }

    var colFav = db.GetCollection<Favourite>("Favourite");
    var favCheck = colFav.Find(x => x.UserId == userCheck.Id).ToList().Select(x => x.GameId);

    return Results.Ok(favCheck);
}).RequireAuthorization();
#endregion

app.Run();


string TokenParse(HttpRequest request)
{
    var jwt = request.Headers["Authorization"];
    var handler = new JwtSecurityTokenHandler();
    string authHeader = jwt.ToString().Replace("Bearer ", "");
    var jsonToken = handler.ReadToken(authHeader);
    var token = handler.ReadToken(authHeader) as JwtSecurityToken;
    var email = token.Claims.First(claim => claim.Type == "email").Value;

    return email;
}
#region Entity
class Favourite
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string UserId { get; set; }
    public int GameId { get; set; }
}
class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public bool Status { get; set; }
}
class UserConfirmation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Code { get; set; }
    public DateTime CreatedDate { get; set; }
}
#endregion

#region DTO
class UserLogin
{
    public string Email { get; set; }
    public string Password { get; set; }
}

class FavouriteDTO
{
    public int GameId { get; set; }
}

class Result
{
    public bool Status { get; set; }
    public string Message { get; set; }
}
class NewToken
{
    public UserDetail UserDetail { get; set; }
    public string Token { get; set; }
    public DateTime ExpireOn { get; set; }
}

class UserDetail
{

    public string Id { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Email { get; set; }
}
#endregion

#region IGDB DTO
class PopularGame
{
    public long? Id { get; set; }
    public string Name { get; set; }
    public double? TotalRating { get; set; }
    public List<string> ImageList { get; set; }
    public string Genre { get; set; }
}

class HypesOfYear
{
    public long? Id { get; set; }
    public string Name { get; set; }
    public double? TotalRating { get; set; }
    public string Cover { get; set; }
    public string Genre { get; set; }
}
#endregion