using System.Text.Json.Serialization;

namespace DIMS.Models
{
    #region Project Models
    public class RedmineProjectResponse
    {
        [JsonPropertyName("project")]
        public RedmineProject Project { get; set; }
    }

    public class RedmineProject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; }

        [JsonPropertyName("parent")]
        public RedmineProjectParent Parent { get; set; }

        [JsonPropertyName("custom_fields")]
        public List<RedmineCustomField> CustomFields { get; set; }

        // Вспомогательный метод для получения значения custom field по id
        public string GetCustomFieldValue(int id)
        {
            return CustomFields?.FirstOrDefault(cf => cf.Id == id)?.Value;
        }

        // Добавляем коллекцию задач (заполняется отдельным запросом)
        [JsonIgnore]
        public List<RedmineIssue> Issues { get; set; } = new List<RedmineIssue>();
    }

    public class RedmineProjectParent
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
    #endregion

    #region Issue Models
    public class RedmineIssuesResponse
    {
        [JsonPropertyName("issues")]
        public List<RedmineIssue> Issues { get; set; }

    }

    public class RedmineIssue
    {
        [JsonPropertyName("project_id")]
        public int ProjectId { get; set; }

        [JsonPropertyName("tracker")]
        public RedmineTracker Tracker { get; set; }

        [JsonPropertyName("assigned_to")]
        public RedmineUser AssignedTo { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("due_date")]
        public DateTime? DueDate { get; set; }

        [JsonPropertyName("estimated_hours")]
        public decimal? EstimatedHours { get; set; }

        [JsonPropertyName("parent")]
        public RedmineIssueParent Parent { get; set; }

        [JsonPropertyName("custom_fields")]
        public List<RedmineCustomField> CustomFields { get; set; }

        // Вспомогательный метод для получения значения custom field по id
        public string GetCustomFieldValue(int id)
        {
            return CustomFields?.FirstOrDefault(cf => cf.Id == id)?.Value;
        }
    }

    public class RedmineIssueParent
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class RedmineCustomField
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class RedmineTracker
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class RedmineUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
    #endregion
}

