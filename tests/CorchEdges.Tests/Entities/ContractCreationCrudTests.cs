using CorchEdges.Data;
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Entities;

[Trait("Category", "EntityCrud")]
[Trait("Entity", "ContractCreation")]
public class ContractCreationCrudTests : EntityCrudTestBase<ContractCreation>
{
    protected override ContractCreation CreateValidEntity()
    {
        return new ContractCreation
        {
            ContractId = $"CONTRACT_{Guid.NewGuid().ToString("N")[..8]}",
            PropertyNo = 123,
            RoomNo = 456,
            ContractorNo = 789,
            PropertyName = "Test Property",
            ContractorName = "John Doe",
            ProgressStatus = "進行中",
            ApplicationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            SecurityDeposit = 160000,
            OutputDateTime = DateTime.Now
        };
    }

    protected override void ModifyEntity(ContractCreation entity)
    {
        entity.ContractorName = "Jane Smith";
        entity.ProgressStatus = "完了";
    }

    protected override DbSet<ContractCreation> GetDbSet(EdgesDbContext context)
    {
        return context.ContractCreations;
    }

    protected override void AssertEntitiesEqual(ContractCreation expected, ContractCreation actual)
    {
        base.AssertEntitiesEqual(expected, actual);
        
        Assert.Equal(expected.ContractId, actual.ContractId);
        Assert.Equal(expected.PropertyNo, actual.PropertyNo);
        Assert.Equal(expected.RoomNo, actual.RoomNo);
        Assert.Equal(expected.ContractorNo, actual.ContractorNo);
        Assert.Equal(expected.PropertyName, actual.PropertyName);
        Assert.Equal(expected.ContractorName, actual.ContractorName);
        Assert.Equal(expected.ProgressStatus, actual.ProgressStatus);
        Assert.Equal(expected.ApplicationDate, actual.ApplicationDate);
        Assert.Equal(expected.SecurityDeposit, actual.SecurityDeposit);
    }

    [Fact]
    [Trait("Validation", "NullableFields")]
    public async Task Create_WithNullableFieldsAsNull_SavesSuccessfully()
    {
        using var dbContext = CreateInMemoryDbContext();
        
        // Arrange
        var entity = new ContractCreation
        {
            // Only set required fields, leave nullable ones as null
            ContractId = $"CONTRACT_MINIMAL_{Guid.NewGuid().ToString("N")[..8]}",
            OutputDateTime = DateTime.Now
        };

        // Act
        dbContext.ContractCreations.Add(entity);
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        
        var savedEntity = await dbContext.ContractCreations
            .FirstOrDefaultAsync(e => e.ContractId == entity.ContractId);
        Assert.NotNull(savedEntity);
        Assert.Equal(entity.ContractId, savedEntity.ContractId);
    }

    [Fact]
    [Trait("Query", "FilterByStatus")]
    public async Task Query_FilterByProgressStatus_ReturnsCorrectEntities()
    {
        using var dbContext = CreateInMemoryDbContext();
        
        // Arrange
        var entities = new[]
        {
            CreateValidEntity(),
            CreateValidEntity(),
            CreateValidEntity()
        };
        
        entities[0].ProgressStatus = "進行中";
        entities[1].ProgressStatus = "完了";
        entities[2].ProgressStatus = "進行中";

        dbContext.ContractCreations.AddRange(entities);
        await dbContext.SaveChangesAsync();

        // Act
        var inProgressContracts = await dbContext.ContractCreations
            .Where(c => c.ProgressStatus == "進行中")
            .ToListAsync();

        // Assert
        Assert.Equal(2, inProgressContracts.Count);
        Assert.All(inProgressContracts, c => Assert.Equal("進行中", c.ProgressStatus));
    }
}