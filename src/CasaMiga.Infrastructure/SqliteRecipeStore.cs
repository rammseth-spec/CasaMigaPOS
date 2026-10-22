using System.Globalization;
using CasaMiga.Domain;
using CasaMiga.Application;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

/// <summary>Technical storage only. Not registered as a user-facing service; authorization is enforced by RecipeService and ProductionService.</summary>
public sealed partial class SqliteRecipeStore(Database database) : IRecipeStore
{
    public void CreateFormula(Formula formula, string name, DateTimeOffset at) => Write((c,t) =>
        Execute(c,t,"INSERT INTO formula(id,code,name,created_utc) VALUES($0,$1,$2,$3)",formula.Id,formula.Code,name,At(at)));

    public IReadOnlyList<FormulaSummary> ListFormulas(int limit = 50, int offset = 0) => Read((c,t) =>
        Rows(c,t,"SELECT id,code,name,active_version_id,revision FROM formula ORDER BY code,id LIMIT $0 OFFSET $1",Limit(limit),Offset(offset))
        .Select(r => new FormulaSummary(G(r,0),S(r,1),S(r,2),NG(r,3),L(r,4))).ToArray());

    public FormulaSummary? GetFormula(Guid id) => Read((c,t) =>
        Rows(c,t,"SELECT id,code,name,active_version_id,revision FROM formula WHERE id=$0",id)
        .Select(r => new FormulaSummary(G(r,0),S(r,1),S(r,2),NG(r,3),L(r,4))).SingleOrDefault());

    public void CreateProfile(ProductProductionProfile profile, DateTimeOffset at) => Write((c,t) =>
        Execute(c,t,"INSERT INTO production_profile(id,product_id,finished_item_id,created_utc) VALUES($0,$1,$2,$3)",profile.Id,profile.ProductId,profile.FinishedInventoryItemId,At(at)));

    public IReadOnlyList<ProfileSummary> ListProfiles(int limit = 50, int offset = 0) => Read((c,t) =>
        Rows(c,t,"SELECT id,product_id,finished_item_id,active_version_id,revision FROM production_profile ORDER BY id LIMIT $0 OFFSET $1",Limit(limit),Offset(offset))
        .Select(r => new ProfileSummary(G(r,0),G(r,1),G(r,2),NG(r,3),L(r,4))).ToArray());

    public ProfileSummary? GetProfile(Guid id) => Read((c,t) =>
        Rows(c,t,"SELECT id,product_id,finished_item_id,active_version_id,revision FROM production_profile WHERE id=$0",id)
        .Select(r => new ProfileSummary(G(r,0),G(r,1),G(r,2),NG(r,3),L(r,4))).SingleOrDefault());

    // Null revision means INSERT; every replacement requires an exact revision, never an upsert.
    public void SaveFormulaDraft(FormulaVersion value, long? expectedRevision = null)
    {
        if (value.State != ProductionVersionState.Draft) throw new InvalidOperationException("Se requiere borrador.");
        Write((c,t) =>
        {
            var pending = HasPendingMeasure(c,t);
            if (!pending && value.PendingMeasure) throw new InvalidOperationException("La base de datos no admite la marca «Por medir».");
            if (expectedRevision is null)
                Execute(c,t,"INSERT INTO formula_version(id,formula_id,version,name,state,yield_dimension,yield_atomic,notes,created_utc) VALUES($0,$1,$2,$3,'DRAFT',$4,$5,$6,$7)",
                    value.Id,value.FormulaId,value.Version,value.Name,Dimension(value.BaseYield.Dimension),value.BaseYield.AtomicUnits,value.Notes,At(value.CreatedAt));
            else
            {
                // Una preparación que ya usan otras recetas o perfiles no puede cambiar de peso a volumen o piezas: se explica en vez de fallar la base.
                if (Rows(c,t,"SELECT 1 FROM formula_version WHERE id=$0 AND yield_dimension<>$1",value.Id,Dimension(value.BaseYield.Dimension)).Count == 1 &&
                    (Rows(c,t,"SELECT 1 FROM profile_formula_requirement WHERE formula_version_id=$0",value.Id).Count != 0 ||
                     HasPreparations(c,t) && Rows(c,t,"SELECT 1 FROM formula_preparation WHERE preparation_version_id=$0",value.Id).Count != 0))
                    throw new ArgumentException("No se puede cambiar el tipo de rendimiento (peso, volumen o piezas) de esta preparación porque ya la usan otras recetas o perfiles. Crea una receta nueva para la otra medida.");
                Changed(Execute(c,t,"UPDATE formula_version SET name=$1,yield_dimension=$2,yield_atomic=$3,notes=$4,revision=revision+1 WHERE id=$0 AND formula_id=$5 AND version=$6 AND created_utc=$7 AND state='DRAFT' AND revision=$8",
                    value.Id,value.Name,Dimension(value.BaseYield.Dimension),value.BaseYield.AtomicUnits,value.Notes,value.FormulaId,value.Version,At(value.CreatedAt),expectedRevision));
                Execute(c,t,"DELETE FROM formula_alternative_option WHERE version_id=$0; DELETE FROM formula_alternative_group WHERE version_id=$0; DELETE FROM formula_ingredient WHERE version_id=$0",value.Id);
                if (HasPreparations(c,t)) Execute(c,t,"DELETE FROM formula_preparation WHERE version_id=$0",value.Id);
            }
            if (pending) Execute(c,t,"UPDATE formula_version SET pending_measure=$1 WHERE id=$0 AND state='DRAFT'",value.Id,value.PendingMeasure ? 1 : 0);
            int sequence = 0;
            foreach (var line in value.Ingredients)
                Execute(c,t,"INSERT INTO formula_ingredient VALUES($0,$1,$2,$3,$4,$5)",value.Id,++sequence,line.InventoryItemId,Dimension(line.Quantity.Dimension),line.Quantity.AtomicUnits,Policy(line.CountPolicy));
            sequence = 0;
            foreach (var line in value.Preparations)
                Execute(c,t,"INSERT INTO formula_preparation VALUES($0,$1,$2,$3,$4)",value.Id,++sequence,line.FormulaVersionId,Dimension(line.Quantity.Dimension),line.Quantity.AtomicUnits);
            sequence = 0;
            foreach (var group in value.Alternatives)
            {
                Execute(c,t,"INSERT INTO formula_alternative_group(version_id,id,sequence) VALUES($0,$1,$2)",value.Id,group.Id,++sequence);
                int optionSequence = 0;
                foreach (var option in group.Options)
                {
                    var rank = group.PreferenceOrder.ToList().IndexOf(option.Id);
                    var line = option.Ingredient;
                    Execute(c,t,"INSERT INTO formula_alternative_option VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8)",value.Id,group.Id,option.Id,++optionSequence,line.InventoryItemId,Dimension(line.Quantity.Dimension),line.Quantity.AtomicUnits,Policy(line.CountPolicy),rank < 0 ? null : rank+1);
                }
            }
        });
    }

    public StoredVersion<FormulaVersion>? GetFormulaVersion(Guid id) => Read((c,t) => ReadFormula(c,t,id));
    public IReadOnlyList<StoredVersion<FormulaVersion>> ListFormulaVersions(Guid formulaId) => Read((c,t) =>
        Rows(c,t,"SELECT id FROM formula_version WHERE formula_id=$0 ORDER BY version",formulaId).Select(r => ReadFormula(c,t,G(r,0))!).ToArray());

    private static StoredVersion<FormulaVersion>? ReadFormula(SqliteConnection c, SqliteTransaction t, Guid id)
    {
        var row = Rows(c,t,"SELECT formula_id,version,name,yield_dimension,yield_atomic,notes,created_utc,published_utc,revision FROM formula_version WHERE id=$0",id).SingleOrDefault();
        if (row is null) return null;
        var ingredients = Rows(c,t,"SELECT item_id,dimension,quantity_atomic,count_policy FROM formula_ingredient WHERE version_id=$0 ORDER BY sequence",id).Select(Ingredient).ToArray();
        var groups = Rows(c,t,"SELECT id FROM formula_alternative_group WHERE version_id=$0 ORDER BY sequence",id).Select(g =>
        {
            var options = Rows(c,t,"SELECT item_id,dimension,quantity_atomic,count_policy,id,preference_rank FROM formula_alternative_option WHERE version_id=$0 AND group_id=$1 ORDER BY sequence",id,G(g,0));
            return new IngredientAlternativeGroup(G(g,0),options.Select(o => new IngredientAlternativeOption(G(o,4),Ingredient(o))),
                options.Where(o => o[5] is not DBNull).OrderBy(o => L(o,5)).Select(o => G(o,4)));
        }).ToArray();
        var preparations = !HasPreparations(c,t) ? Array.Empty<FormulaPreparation>() : Rows(c,t,"SELECT preparation_version_id,dimension,quantity_atomic FROM formula_preparation WHERE version_id=$0 ORDER BY sequence",id)
            .Select(p => new FormulaPreparation(G(p,0),Quantity(p,1,2))).ToArray();
        var pendingMeasure = HasPendingMeasure(c,t) && L(Rows(c,t,"SELECT pending_measure FROM formula_version WHERE id=$0",id).Single(),0) == 1;
        var value = new FormulaVersion(id,G(row,0),checked((int)L(row,1)),S(row,2),Quantity(row,3,4),ingredients,groups,Date(row,6),row[5] as string,preparations,pendingMeasure);
        if (row[7] is not DBNull) value = new Formula(value.FormulaId,"REHYDRATION").PublishVersion(value,Date(row,7)).Versions.Single();
        return new(value,L(row,8));
    }

    public void SaveProfileDraft(ProductProductionProfileVersion value, long? expectedRevision = null)
    {
        if (value.State != ProductionVersionState.Draft) throw new InvalidOperationException("Se requiere borrador.");
        Write((c,t) =>
        {
            if (Rows(c,t,"SELECT 1 FROM production_profile WHERE id=$0 AND product_id=$1 AND finished_item_id=$2",value.ProfileId,value.ProductId,value.FinishedInventoryItemId).Count != 1)
                throw new InvalidOperationException("El perfil no corresponde al producto/artículo.");
            if (expectedRevision is null)
                Execute(c,t,"INSERT INTO production_profile_version(id,profile_id,version,name,state,created_utc) VALUES($0,$1,$2,$3,'DRAFT',$4)",value.Id,value.ProfileId,value.Version,value.Name,At(value.CreatedAt));
            else
            {
                Changed(Execute(c,t,"UPDATE production_profile_version SET name=$1,revision=revision+1 WHERE id=$0 AND profile_id=$2 AND version=$3 AND created_utc=$4 AND state='DRAFT' AND revision=$5",value.Id,value.Name,value.ProfileId,value.Version,At(value.CreatedAt),expectedRevision));
                Execute(c,t,"DELETE FROM profile_formula_requirement WHERE profile_version_id=$0; DELETE FROM profile_direct_ingredient WHERE profile_version_id=$0",value.Id);
            }
            int sequence = 0;
            foreach (var line in value.Formulas)
                Execute(c,t,"INSERT INTO profile_formula_requirement VALUES($0,$1,$2,$3,$4)",value.Id,line.FormulaVersionId,++sequence,Dimension(line.QuantityPerPiece.Dimension),line.QuantityPerPiece.AtomicUnits);
            sequence = 0;
            foreach (var direct in value.DirectIngredients)
            {
                var line = direct.Ingredient;
                Execute(c,t,"INSERT INTO profile_direct_ingredient VALUES($0,$1,$2,$3,$4,$5)",value.Id,++sequence,line.InventoryItemId,Dimension(line.Quantity.Dimension),line.Quantity.AtomicUnits,Policy(line.CountPolicy));
            }
        });
    }

    public StoredVersion<ProductProductionProfileVersion>? GetProfileVersion(Guid id) => Read((c,t) => ReadProfile(c,t,id));
    public IReadOnlyList<StoredVersion<ProductProductionProfileVersion>> ListProfileVersions(Guid profileId) => Read((c,t) =>
        Rows(c,t,"SELECT id FROM production_profile_version WHERE profile_id=$0 ORDER BY version",profileId).Select(r => ReadProfile(c,t,G(r,0))!).ToArray());

    private static StoredVersion<ProductProductionProfileVersion>? ReadProfile(SqliteConnection c, SqliteTransaction t, Guid id)
    {
        var r = Rows(c,t,"SELECT v.profile_id,v.version,v.name,v.created_utc,v.published_utc,v.revision,p.product_id,p.finished_item_id FROM production_profile_version v JOIN production_profile p ON p.id=v.profile_id WHERE v.id=$0",id).SingleOrDefault();
        if (r is null) return null;
        var formulas = Rows(c,t,"SELECT formula_version_id,dimension,quantity_atomic FROM profile_formula_requirement WHERE profile_version_id=$0 ORDER BY sequence",id)
            .Select(x => new FormulaRequirement(G(x,0),Quantity(x,1,2)));
        var direct = Rows(c,t,"SELECT item_id,dimension,quantity_atomic,count_policy FROM profile_direct_ingredient WHERE profile_version_id=$0 ORDER BY sequence",id)
            .Select(x => new DirectIngredientRequirement(Ingredient(x)));
        var value = new ProductProductionProfileVersion(id,G(r,0),checked((int)L(r,1)),G(r,6),G(r,7),S(r,2),formulas,direct,Date(r,3));
        if (r[4] is not DBNull) value = new ProductProductionProfile(value.ProfileId,value.ProductId,value.FinishedInventoryItemId).PublishVersion(value,Date(r,4)).Versions.Single();
        return new(value,L(r,5));
    }

    public void PublishFormula(Guid id, long expectedRevision, DateTimeOffset at) => Write((c,t) =>
    {
        var version = ReadFormula(c,t,id) ?? throw new KeyNotFoundException();
        _ = new Formula(version.Value.FormulaId,"VALIDATION").PublishVersion(version.Value,at);
        // Lo mismo revisa la base al publicar; aquí se explica con los nombres.
        var unpublished = version.Value.Preparations.Select(p => Rows(c,t,"SELECT f.name,v.version,v.state FROM formula_version v JOIN formula f ON f.id=v.formula_id WHERE v.id=$0",p.FormulaVersionId).Single())
            .Where(r => S(r,2) != "PUBLISHED").Select(r => $"{S(r,0)} · versión {L(r,1)}").ToArray();
        if (unpublished.Length != 0) throw new ArgumentException($"Publica primero las preparaciones que usa esta receta: {string.Join(", ",unpublished)}.");
        Publish(c,t,"formula_version",id,expectedRevision,at);
    });
    public void PublishProfile(Guid id, long expectedRevision, DateTimeOffset at) => Write((c,t) =>
    {
        var version = ReadProfile(c,t,id) ?? throw new KeyNotFoundException();
        _ = new ProductProductionProfile(version.Value.ProfileId,version.Value.ProductId,version.Value.FinishedInventoryItemId).PublishVersion(version.Value,at);
        Publish(c,t,"production_profile_version",id,expectedRevision,at);
    });
    private static void Publish(SqliteConnection c, SqliteTransaction t, string table, Guid id, long revision, DateTimeOffset at) =>
        Changed(Execute(c,t,$"UPDATE {table} SET state='PUBLISHED',published_utc=$1,revision=revision+1 WHERE id=$0 AND state='DRAFT' AND revision=$2",id,At(at),revision));

    public void ActivateFormula(Guid formulaId, Guid? versionId, long expectedRevision) => Write((c,t) => Activate(c,t,"formula",formulaId,versionId,expectedRevision));
    public void ActivateProfile(Guid profileId, Guid? versionId, long expectedRevision) => Write((c,t) => Activate(c,t,"production_profile",profileId,versionId,expectedRevision));
    private static void Activate(SqliteConnection c, SqliteTransaction t, string table, Guid id, Guid? versionId, long revision) =>
        Changed(Execute(c,t,$"UPDATE {table} SET active_version_id=$1,revision=revision+1 WHERE id=$0 AND revision=$2",id,versionId,revision));

    private SqliteConnection? scopedConnection;
    private SqliteTransaction? scopedTransaction;
    internal SqliteRecipeStore(Database database, SqliteConnection connection, SqliteTransaction transaction) : this(database)
    { scopedConnection=connection; scopedTransaction=transaction; }

    private void Write(Action<SqliteConnection,SqliteTransaction> action)
    {
        if(scopedConnection is not null){action(scopedConnection,scopedTransaction!);return;}
        using var c = database.Open(); using var t = c.BeginTransaction(deferred:false);
        action(c,t); t.Commit();
    }
    private T Read<T>(Func<SqliteConnection,SqliteTransaction,T> action)
    {
        if(scopedConnection is not null)return action(scopedConnection,scopedTransaction!);
        using var c = database.Open(readOnly:true); using var t = c.BeginTransaction(deferred:true);
        var result = action(c,t); t.Commit(); return result;
    }
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction t, string sql, params object?[] values)
    {
        var cmd = c.CreateCommand(); cmd.Transaction=t; cmd.CommandText=sql;
        for (int i=0;i<values.Length;i++) cmd.Parameters.AddWithValue("$"+i,values[i] is Guid id ? id.ToString("D") : values[i] ?? DBNull.Value);
        return cmd;
    }
    private static int Execute(SqliteConnection c, SqliteTransaction t, string sql, params object?[] values)
    { using var cmd=Command(c,t,sql,values); return cmd.ExecuteNonQuery(); }
    private static List<object[]> Rows(SqliteConnection c, SqliteTransaction t, string sql, params object?[] values)
    {
        using var cmd=Command(c,t,sql,values); using var reader=cmd.ExecuteReader(); var rows=new List<object[]>();
        while (reader.Read()) { var row=new object[reader.FieldCount]; reader.GetValues(row); rows.Add(row); } return rows;
    }
    // Una base anterior a la migración 031 (copias de prueba de migraciones viejas) no tiene preparaciones dentro de preparaciones.
    private static bool HasPreparations(SqliteConnection c, SqliteTransaction t) => Rows(c,t,"SELECT 1 FROM sqlite_master WHERE type='table' AND name='formula_preparation'").Count == 1;
    private static bool HasPendingMeasure(SqliteConnection c, SqliteTransaction t) => Rows(c,t,"SELECT 1 FROM pragma_table_info('formula_version') WHERE name='pending_measure'").Count == 1;
    private static void Changed(int count) { if(count!=1) throw new RecipeConcurrencyException(); }
    private static int Limit(int value) => value is >=1 and <=200 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static int Offset(int value) => value>=0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string At(DateTimeOffset at) => at.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
    private static string S(object[] row,int i) => (string)row[i];
    private static long L(object[] row,int i) => (long)row[i];
    private static Guid G(object[] row,int i) => Guid.Parse(S(row,i));
    private static Guid? NG(object[] row,int i) => row[i] is DBNull ? null : G(row,i);
    private static DateTimeOffset Date(object[] row,int i) => DateTimeOffset.Parse(S(row,i),CultureInfo.InvariantCulture);
    private static string Dimension(InventoryDimension value) => value.ToString().ToUpperInvariant();
    private static InventoryQuantity Quantity(object[] row,int dimension,int quantity) => new(Enum.Parse<InventoryDimension>(S(row,dimension),true),L(row,quantity));
    private static FormulaIngredient Ingredient(object[] row) => new(G(row,0),Quantity(row,1,2),ParsePolicy(S(row,3)));
    private static string Policy(IngredientCountPolicy value) => value switch
    {
        IngredientCountPolicy.NotApplicable => "NOT_APPLICABLE", IngredientCountPolicy.ExactWholeCount => "EXACT_WHOLE_COUNT",
        IngredientCountPolicy.CeilingWholeCount => "CEILING_WHOLE_COUNT", IngredientCountPolicy.Unspecified => "UNSPECIFIED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static IngredientCountPolicy ParsePolicy(string value) => value switch
    {
        "NOT_APPLICABLE" => IngredientCountPolicy.NotApplicable, "EXACT_WHOLE_COUNT" => IngredientCountPolicy.ExactWholeCount,
        "CEILING_WHOLE_COUNT" => IngredientCountPolicy.CeilingWholeCount, "UNSPECIFIED" => IngredientCountPolicy.Unspecified,
        _ => throw new InvalidOperationException("Política almacenada inválida.")
    };
}
