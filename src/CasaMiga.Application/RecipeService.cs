using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed class RecipeService(IProductionUnitOfWork work,UserSession user,IClock clock,Guid terminal)
{
    private T Read<T>(Func<IRecipeStore,T> read)=>work.Run(s=>{Authorize(s,"recipe.view");return read(s.Recipes);});
    private Guid Authorize(IProductionSession s,string permission)
    {
        var actor=user.Current?.Id??throw new AccessDeniedException();
        if(!s.Authorized(actor,terminal,permission))throw new AccessDeniedException();return actor;
    }
    private void Write(Guid id,string action,Action<IRecipeStore> write)=>work.Run(s=>{
        var actor=Authorize(s,"recipe.configure");write(s.Recipes);s.Audit(actor,terminal,action,id,clock.UtcNow);return true;
    });
    public FormulaSummary? GetFormula(Guid id)=>Read(s=>s.GetFormula(id));
    public ProfileSummary? GetProfile(Guid id)=>Read(s=>s.GetProfile(id));
    public IReadOnlyList<FormulaSummary> ListFormulas(int limit=50,int offset=0)=>Read(s=>s.ListFormulas(limit,offset));
    public IReadOnlyList<ProfileSummary> ListProfiles(int limit=50,int offset=0)=>Read(s=>s.ListProfiles(limit,offset));
    public StoredVersion<FormulaVersion>? GetFormulaVersion(Guid id)=>Read(s=>s.GetFormulaVersion(id));
    public StoredVersion<ProductProductionProfileVersion>? GetProfileVersion(Guid id)=>Read(s=>s.GetProfileVersion(id));
    public IReadOnlyList<StoredVersion<FormulaVersion>> ListFormulaVersions(Guid id)=>Read(s=>s.ListFormulaVersions(id));
    public IReadOnlyList<StoredVersion<ProductProductionProfileVersion>> ListProfileVersions(Guid id)=>Read(s=>s.ListProfileVersions(id));
    public void CreateFormula(Formula value,string name)=>Write(value.Id,"recipe.formula.created",s=>s.CreateFormula(value,name,clock.UtcNow));
    public void CreateProfile(ProductProductionProfile value)=>Write(value.Id,"recipe.profile.created",s=>s.CreateProfile(value,clock.UtcNow));
    public void SaveFormulaDraft(FormulaVersion value,long? revision=null)=>Write(value.Id,"recipe.formula.draft_saved",s=>s.SaveFormulaDraft(value,revision));
    public void SaveProfileDraft(ProductProductionProfileVersion value,long? revision=null)=>Write(value.Id,"recipe.profile.draft_saved",s=>s.SaveProfileDraft(value,revision));
    public void PublishFormula(Guid id,long revision)=>Write(id,"recipe.formula.published",s=>s.PublishFormula(id,revision,clock.UtcNow));
    public void PublishProfile(Guid id,long revision)=>Write(id,"recipe.profile.published",s=>s.PublishProfile(id,revision,clock.UtcNow));
    public void ActivateFormula(Guid id,Guid? version,long revision)=>Write(id,"recipe.formula.activated",s=>s.ActivateFormula(id,version,revision));
    public void ActivateProfile(Guid id,Guid? version,long revision)=>Write(id,"recipe.profile.activated",s=>s.ActivateProfile(id,version,revision));
}
