using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpSkillTools
    {
        public McpSkillTools(McpSkillService skills) => _skills = skills;

        [McpServerTool(Name = "route_skills")]
        public string RouteSkills(string task) => JsonSerializer.Serialize(_skills.Router.Route(task));
        [McpServerTool(Name = "load_skills")]
        public string LoadSkills(string[] names) => JsonSerializer.Serialize(names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(System.StringComparer.OrdinalIgnoreCase).Select(_skills.Store.Get).Where(x => x.Enabled));
        [McpServerTool(Name = "load_enabled_skills")]
        public string LoadEnabledSkills() => JsonSerializer.Serialize(_skills.Store.ListEnabled());
        [McpServerTool(Name = "list_skills")]
        public string ListSkills() => JsonSerializer.Serialize(_skills.Store.List());
        [McpServerTool(Name = "get_skill")]
        public string GetSkill(string name) => JsonSerializer.Serialize(_skills.Store.Get(name));
        [McpServerTool(Name = "set_skill_enabled")]
        public string SetSkillEnabled(string name, bool enabled) => JsonSerializer.Serialize(_skills.Store.SetEnabled(name, enabled));
        [McpServerTool(Name = "create_skill")]
        public string CreateSkill(string name, string content) => JsonSerializer.Serialize(_skills.Store.Create(name, content));
        [McpServerTool(Name = "update_skill")]
        public string UpdateSkill(string name, string content) => JsonSerializer.Serialize(_skills.Store.Update(name, content));
        [McpServerTool(Name = "delete_skill")]
        public string DeleteSkill(string name) => JsonSerializer.Serialize(new { deleted = _skills.Store.Delete(name), name });
        [McpServerTool(Name = "install_skill")]
        public async Task<string> InstallSkill(string source, bool enabled = true, string name = null, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _skills.InstallAsync(source, enabled, name, cancellationToken).ConfigureAwait(false));
        [McpServerTool(Name = "check_skill_updates")]
        public async Task<string> CheckSkillUpdates(string name = null, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _skills.CheckUpdatesAsync(name, cancellationToken).ConfigureAwait(false));
        [McpServerTool(Name = "update_skill_from_source")]
        public async Task<string> UpdateSkillFromSource(string name, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _skills.UpdateFromSourceAsync(name, cancellationToken).ConfigureAwait(false));

        private readonly McpSkillService _skills;
    }
}
