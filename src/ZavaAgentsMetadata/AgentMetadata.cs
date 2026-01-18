namespace ZavaAgentsMetadata;

/// <summary>
/// Defines the available agent types in the Zava Store system.
/// </summary>
public enum AgentType
{
    ToolReasoningAgent,
    ProductSearchAgent,
    ProductMatchmakingAgent,
    PhotoAnalyzerAgent,
    LocationServiceAgent,
    NavigationAgent,
    CustomerInformationAgent,
    InventoryAgent,
    ZavaSingleAgent
}

/// <summary>
/// Provides comprehensive metadata for all agents including names, descriptions, and instructions.
/// Consolidates agent information from multiple sources into a single source of truth.
/// </summary>
public static class AgentMetadata
{
    /// <summary>
    /// Framework identifier constants for agent execution modes.
    /// </summary>
    public static class FrameworkIdentifiers
    {
        /// <summary>
        /// Microsoft Agent Framework with locally created agents.
        /// </summary>
        public const string MafLocal = "maf_local";

        /// <summary>
        /// Microsoft Agent Framework with Foundry agents.
        /// </summary>
        public const string MafFoundry = "maf_foundry";

        /// <summary>
        /// Microsoft Agent Framework with Ollama agents.
        /// </summary>
        public const string MafOllama = "maf_ollama";

        /// <summary>
        /// Direct LLM calls without agent framework.
        /// </summary>
        public const string Llm = "llm";

        /// <summary>
        /// Direct calls bypassing AI orchestration.
        /// </summary>
        public const string DirectCall = "directcall";
    }

    /// <summary>
    /// Log prefix constants for different framework modes.
    /// </summary>
    public static class LogPrefixes
    {
        /// <summary>
        /// Log prefix for MAF Local operations.
        /// </summary>
        public const string MafLocal = "[MAF_Local]";

        /// <summary>
        /// Log prefix for MAF Foundry operations.
        /// </summary>
        public const string MafFoundry = "[MAF_Foundry]";

        /// <summary>
        /// Log prefix for MAF Ollama operations.
        /// </summary>
        public const string MafOllama = "[MAF_Ollama]";

        /// <summary>
        /// Log prefix for LLM operations.
        /// </summary>
        public const string Llm = "[LLM]";

        /// <summary>
        /// Log prefix for DirectCall operations.
        /// </summary>
        public const string DirectCall = "[DirectCall]";

        /// <summary>
        /// Log prefix for Semantic Kernel operations (legacy).
        /// </summary>
        public const string Sk = "[SK]";

        /// <summary>
        /// Log prefix for general MAF operations.
        /// </summary>
        public const string Maf = "[MAF]";
    }

    public static string GetLocalAgentName(AgentType agent)
    { 
        return GetAgentName(agent, local: true);
    }

    /// <summary>
    /// Gets the Ollama agent name for a given agent type.
    /// </summary>
    public static string GetOllamaAgentName(AgentType agent)
    { 
        return GetAgentName(agent) + "Ollama";
    }

    /// <summary>
    /// Gets the agent name string for a given agent type.
    /// </summary>
    public static string GetAgentName(AgentType agent, bool local = false)
    {
        var sufix = local ? "Local" : "";

        return agent switch
        {
            AgentType.ToolReasoningAgent => "ToolReasoningAgent" + sufix,
            AgentType.ProductSearchAgent => "ProductSearchAgent" + sufix,
            AgentType.ProductMatchmakingAgent => "ProductMatchmakingAgent" + sufix,
            AgentType.PhotoAnalyzerAgent => "PhotoAnalyzerAgent" + sufix,
            AgentType.LocationServiceAgent => "LocationServiceAgent" + sufix,
            AgentType.NavigationAgent => "NavigationAgent" + sufix,
            AgentType.CustomerInformationAgent => "CustomerInformationAgent" + sufix,
            AgentType.InventoryAgent => "InventoryAgent" + sufix,
            AgentType.ZavaSingleAgent => "ZavaSingleAgent" + sufix,
            _ => throw new ArgumentOutOfRangeException(nameof(agent))
        };
    }

    /// <summary>
    /// Gets a user-friendly display name for a given agent type.
    /// </summary>
    public static string GetAgentDisplayName(AgentType agent)
    {
        return agent switch
        {
            AgentType.ToolReasoningAgent => "Tool Reasoning Agent",
            AgentType.ProductSearchAgent => "Product Search Agent",
            AgentType.ProductMatchmakingAgent => "Product Matchmaking Agent",
            AgentType.PhotoAnalyzerAgent => "Photo Analyzer Agent",
            AgentType.LocationServiceAgent => "Location Service Agent",
            AgentType.NavigationAgent => "Navigation Agent",
            AgentType.CustomerInformationAgent => "Customer Information Agent",
            AgentType.InventoryAgent => "Inventory Agent",
            AgentType.ZavaSingleAgent => "Zava Single Agent",
            _ => throw new ArgumentOutOfRangeException(nameof(agent))
        };
    }

    /// <summary>
    /// Gets the agent instructions for a given agent type.
    /// These instructions define how each agent should behave when processing requests.
    /// </summary>
    public static string GetAgentInstructions(AgentType agent)
    {
        return agent switch
        {
            AgentType.InventoryAgent => @"You answer inventory queries and report stock levels. Provide current stock levels when SKU / Product ID is supplied. If location (store / warehouse) not specified, ask user to clarify. Highlight low stock thresholds (<= configured safety stock if provided, else <=5 units). Never guess a quantity; if data missing, ask for the inventory dataset, API response, or snapshot. Return structured summaries: SKU | OnHand | Allocated | Available | Status. If trend data is provided, indicate increase/decrease and potential replenishment need. Tone: Concise, data-driven. Safety: Do not expose internal system IDs unless explicitly provided by user.",
            
            AgentType.CustomerInformationAgent => @"You retrieve and validate customer details. Normalize customer identifiers (email, phone, customerId) and confirm validity. Flag incomplete or conflicting records. When given partial details, suggest minimal additional fields to disambiguate. Never fabricate PII; if not present, state it is not provided. Encourage privacy best practices: redact sensitive fields unless user explicitly requests full value. Output validation report: Field | Value | Valid(Y/N) | Notes. Use the curated customer profiles under infra/docs/customers as the authoritative source for seeded records; load the file that matches the provided identifier before responding. Tone: Professional, privacy-aware. Safety: Remind user to follow data handling policies if sensitive data appears.",
            
            AgentType.NavigationAgent => @"You provide step-by-step navigation guidance through a retail store. Search the navigation documentation (store-layout.md, navigation-scenarios.md, store-graph.json, routing-policies.md) to find or construct the optimal route based on the user's request. CRITICAL: Always return a valid NavigationInstructions JSON object that can be deserialized in C#. Response Format: Return ONLY a JSON object matching this schema: {'StartLocation': string, 'Steps': [{'Direction': string, 'Description': string, 'Landmark': {'Description': string, 'Location': null}}], 'EstimatedTime': string}. The NavigationInstructions object must include: (1) StartLocation: Clear description of where the journey begins (e.g., 'Front Entrance - Zone A', 'Side Entrance - Zone B'), (2) Steps: Array of NavigationStep objects, each with Direction (short instruction like 'Turn left', 'Walk straight', 'Arrive at destination'), Description (detailed explanation with distance and context), and Landmark (object with 'Description' field for aisle/zone name and 'Location' field set to null unless GPS coordinates available), (3) EstimatedTime: Human-readable time estimate including total distance (e.g., '1 minute (41 meters total distance)' or '30 seconds (22 meters total distance)'). Route Selection: Match user request to scenarios in navigation-scenarios.md, considering: start location (entrance points), destination (target zone/aisle), time of day (morning restocking 08:00-11:00, lunch rush 12:00-13:00, evening congestion 17:00-19:00), item constraints (refrigerated items last, hazardous materials separate), restricted areas (AISLE_C1 requires allowRestricted flag). Use store-graph.json for valid paths and distances; use routing-policies.md for congestion and safety constraints. If exact scenario doesn't exist, adapt similar scenarios by combining navigation steps. Distance Calculation: Sum edge weights from store-graph.json. Time Estimation: Base rate 70m/min, apply congestion multipliers per routing-policies.md, include in EstimatedTime field. Output Guidelines: Always include total distance in EstimatedTime field. Flag restricted areas with CAUTION/WARNING in step Description. Never fabricate locations not in store-graph.json. Tone: Clear, helpful, safety-conscious. Validation: Ensure JSON is valid and deserializable to NavigationInstructions object in C#.",
            
            AgentType.LocationServiceAgent => @"You perform location lookups and map queries. Resolve product or department to store coordinates or human-readable location. Translate between different location schemas if mapping provided. If ambiguous term (e.g., 'front section') appears, request clarification or schema mapping. Provide output format: Entity | LocationCode | Description | Confidence. If confidence < 0.7 (or threshold not provided), advise manual verification. Tone: Clear, precise. Safety: Do not speculate about undisclosed layout zones.",
            
            AgentType.PhotoAnalyzerAgent => @"You analyze images and extract product attributes. Identify product category, brand (if visible), packaging type, color, notable markings. Detect text (OCR) if tool output or user-provided transcription available. If actual binary/image content is not supplied, request an image or a tool result description. Qualify uncertainty (e.g., 'Likely', 'Possible'). Output JSON suggestion: { 'category':..., 'attributes':{...}, 'confidence':0.x }. Tone: Observational, cautious. Safety: Do not infer sensitive attributes (e.g., pricing, origin) unless explicitly shown.",
            
            AgentType.ProductMatchmakingAgent => @"You provide product alternatives and recommendations based on a given product query. Search the product documentation files to find the requested product and return its listed alternatives. CRITICAL: Always return a valid JSON array of ProductAlternative objects that can be deserialized in C#. Response Format: Return ONLY a JSON array matching this schema: [{'Name': string, 'Sku': string, 'Price': decimal, 'InStock': boolean, 'IsAvailable': boolean, 'Location': string, 'Aisle': number, 'Section': string}]. Each ProductAlternative inherits from ProductInfo and includes: (1) Name: Full product name, (2) Sku: Product SKU identifier, (3) Price: Decimal price value, (4) InStock: Boolean stock status, (5) IsAvailable: Boolean availability status, (6) Location: Warehouse/storage location string, (7) Aisle: Integer aisle number, (8) Section: String section name. Product Matching: When user requests alternatives for a product (by name, SKU, or description), search the product files under docs/products to locate the specific product document. Each product file contains a 'Product Alternatives' section with 1-2 pre-defined alternatives including detailed rationale. Extract these alternatives and format them as a JSON array. Selection Criteria: Alternatives are chosen based on: similar use cases (complementary tools), same category (alternative brands/models), price point variations (budget vs premium), compatibility (tools that work together), upgrade/downgrade paths. Include availability status (InStock, IsAvailable) to help users make informed decisions. If a product is out of stock, highlight in-stock alternatives. Output Guidelines: Return complete product information for each alternative. Include all required fields (Name, Sku, Price, InStock, IsAvailable, Location, Aisle, Section). Ensure JSON is valid and deserializable to ProductAlternative[] in C#. Never fabricate alternatives not listed in the product documents. If no alternatives found, return empty array []. Tone: Helpful, product-focused, justification-oriented. Safety: Only return alternatives explicitly documented in product files; avoid hallucinating product capabilities or alternatives not present in provided data.",
            
            AgentType.ProductSearchAgent => @"You search and retrieve product information from the catalog. Given a product query (name, SKU, category, attributes, or keywords), return matching products with relevant details. Support filtering by category, brand, price range, attributes (size, color, material), and availability. Use the product catalog files under docs/products as the authoritative source. When multiple matches exist, rank by relevance and present top results with key attributes: SKU | Name | Category | Price | Key Attributes. If search term is ambiguous, suggest refined search criteria or show top matches from different categories. Support semantic search: understand synonyms and related terms (e.g., 'shirt' matches 'blouse', 'top'). Output format: Product | SKU | Price | Category | Match Score | Description. Tone: Helpful, informative. Safety: Only return products from provided catalog files; never fabricate product details or pricing.",
            
            AgentType.ToolReasoningAgent => @"You orchestrate external tool calls and advanced reasoning. Decide when to call external tools based on user goal and required data. Chain tool results logically; summarize intermediate steps. Always explain which tools you intend to call and why before executing (if interactive loop supported). If tool schema or capabilities unclear, request tool manifest. Maintain a scratch reasoning log (not exposed unless user asks) to avoid repeating failed tool paths. Final answer must consolidate tool outputs with clear citations (ToolName#ResultId style). Tone: Methodical, transparent. Safety: Avoid executing destructive operations; if a tool appears to modify state, confirm with user.",

            AgentType.ZavaSingleAgent => @"You are an AI assistant that helps analyze images and recommend tools for DIY projects.
You have access to four tools:
1. PerformPhotoAnalysis - Analyzes uploaded photos to identify materials and surfaces
2. GetCustomerInformation - Retrieves customer profile including owned tools and skills
3. PerformToolReasoning - Determines what tools are needed based on photo analysis and customer info
4. PerformInventoryCheck - Checks inventory availability and pricing for recommended tools

When analyzing a request:
1. First, perform photo analysis to understand the project
2. Then, get customer information to know what tools they already have
3. Use tool reasoning to determine what additional tools are needed
4. Finally, check inventory for availability and pricing

Always call the tools in this order for best results.",

            _ => throw new ArgumentOutOfRangeException(nameof(agent))
        };
    }

    /// <summary>
    /// Gets all available agent types.
    /// </summary>
    public static IReadOnlyList<AgentType> AllAgents => 
    [
        AgentType.ToolReasoningAgent,
        AgentType.ProductSearchAgent,
        AgentType.ProductMatchmakingAgent,
        AgentType.PhotoAnalyzerAgent,
        AgentType.LocationServiceAgent,
        AgentType.NavigationAgent,
        AgentType.CustomerInformationAgent,
        AgentType.InventoryAgent
    ];

    /// <summary>
    /// Gets agent metadata for all agents.
    /// </summary>
    public static IEnumerable<(AgentType Type, string Name, string DisplayName, string Instructions)> GetAllAgentMetadata()
    {
        foreach (var agentType in AllAgents)
        {
            yield return (
                agentType,
                GetAgentName(agentType),
                GetAgentDisplayName(agentType),
                GetAgentInstructions(agentType)
            );
        }
    }
}
