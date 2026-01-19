using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Entities;

[Trait("Category", TestCategories.Entity)]
[Trait("Entity", "ContractCurrent")]
public class ContractCurrentCrudTests : EntityCrudTestBase<ContractCurrent>
{
    protected override ContractCurrent CreateValidEntity(ProcessedFile processedFile)
    {
        return new ContractCurrent
        {
            ContractId = $"CURRENT_{Guid.NewGuid().ToString("N")[..8]}",
            ResidentCode = $"RES_{Guid.NewGuid().ToString("N")[..6]}",
            ContractTypeName = "Regular Contract",
            PropertyName = "Test Apartment Complex",
            RoomType = "1LDK",
            PropertyNo = 100,
            RoomNo = 201,
            ContractorNo = 1001,
            ContractorName = "Tanaka Taro",
            ResidentName = "Tanaka Taro",
            ContractStatus = "有効",
            MoveInDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-365)),
            ContractStartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-365)),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(365)),
            Rent = 90000,
            ManagementFee = 8000,
            WaterFee = 3000,
            ElectricityFee = 0,
            SecurityDeposit = 180000,
            FixedTermLease = false,
            ContractorEmail = "tanaka@example.com",
            ContractorPhoneNumber = "090-1234-5678",
            OutputDateTime = DateTime.Now,
            ProcessedFile = processedFile
        };
    }

    protected override void ModifyEntity(ContractCurrent entity)
    {
        entity.ContractorName = "Tanaka Hanako";
        entity.ResidentName = "Tanaka Hanako";
        entity.Rent = 95000;
        entity.ManagementFee = 8500;
        entity.ContractorEmail = "hanako@example.com";
        entity.ContractorPhoneNumber = "090-9876-5432";
    }

    protected override DbSet<ContractCurrent> GetDbSet(EdgesDbContext context)
    {
        return context.ContractCurrents;
    }

    protected override void AssertEntitiesEqual(ContractCurrent expected, ContractCurrent actual)
    {
        base.AssertEntitiesEqual(expected, actual);
        
        Assert.Equal(expected.ContractId, actual.ContractId);
        Assert.Equal(expected.ResidentCode, actual.ResidentCode);
        Assert.Equal(expected.ContractTypeName, actual.ContractTypeName);
        Assert.Equal(expected.PropertyName, actual.PropertyName);
        Assert.Equal(expected.RoomType, actual.RoomType);
        Assert.Equal(expected.PropertyNo, actual.PropertyNo);
        Assert.Equal(expected.RoomNo, actual.RoomNo);
        Assert.Equal(expected.ContractorNo, actual.ContractorNo);
        Assert.Equal(expected.ContractorName, actual.ContractorName);
        Assert.Equal(expected.ResidentName, actual.ResidentName);
        Assert.Equal(expected.ContractStatus, actual.ContractStatus);
        Assert.Equal(expected.Rent, actual.Rent);
        Assert.Equal(expected.ManagementFee, actual.ManagementFee);
        Assert.Equal(expected.SecurityDeposit, actual.SecurityDeposit);
        Assert.Equal(expected.FixedTermLease, actual.FixedTermLease);
    }

    [Fact]
    [Trait("Query", "FilterByProperty")]
    public async Task Query_FilterByPropertyNo_ReturnsCorrectEntities()
    {
        using var dbContext = CreateInMemoryDbContext();
        var processedFile = await CreateProcessedFileAsync(dbContext);

        // Arrange
        var entities = new[]
        {
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile)
        };
        
        entities[0].PropertyNo = 100;
        entities[1].PropertyNo = 200;
        entities[2].PropertyNo = 100;

        dbContext.ContractCurrents.AddRange(entities);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var property100Contracts = await dbContext.ContractCurrents
            .Where(c => c.PropertyNo == 100)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, property100Contracts.Count);
        Assert.All(property100Contracts, c => Assert.Equal(100, c.PropertyNo));
    }

    [Fact]
    [Trait("Query", "FilterByRentRange")]
    public async Task Query_FilterByRentRange_ReturnsCorrectEntities()
    {
        using var dbContext = CreateInMemoryDbContext();
        var processedFile = await CreateProcessedFileAsync(dbContext);

        // Arrange
        var entities = new[]
        {
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile)
        };
        
        entities[0].Rent = 80000;
        entities[1].Rent = 120000;
        entities[2].Rent = 95000;

        dbContext.ContractCurrents.AddRange(entities);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var affordableRents = await dbContext.ContractCurrents
            .Where(c => c.Rent <= 100000)
            .Select(contract => contract.Rent)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, affordableRents.Count);
        Assert.Contains(80000, affordableRents);
        Assert.Contains(95000, affordableRents);
    }

    [Fact]
    [Trait("Validation", "ContractStatus")]
    public async Task Create_WithValidContractStatus_SavesSuccessfully()
    {
        using var dbContext = CreateInMemoryDbContext();
        var processedFile = await CreateProcessedFileAsync(dbContext);

        // Arrange
        var entity = CreateValidEntity(processedFile);
        entity.ContractStatus = "解約予定";

        // Act
        dbContext.ContractCurrents.Add(entity);
        var result = await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result);
        
        var savedEntity = await dbContext.ContractCurrents
            .FirstOrDefaultAsync(e => e.ContractId == entity.ContractId, TestContext.Current.CancellationToken);
        Assert.NotNull(savedEntity);
        Assert.Equal("解約予定", savedEntity.ContractStatus);
    }

    [Fact]
    [Trait("Query", "FilterByFixedTermLease")]
    public async Task Query_FilterByFixedTermLease_ReturnsCorrectEntities()
    {
        using var dbContext = CreateInMemoryDbContext();
        var processedFile = await CreateProcessedFileAsync(dbContext);
   
        // Arrange
        var entities = new[]
        {
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile),
            CreateValidEntity(processedFile)
        };
        
        entities[0].FixedTermLease = true;
        entities[1].FixedTermLease = false;
        entities[2].FixedTermLease = true;

        dbContext.ContractCurrents.AddRange(entities);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var fixedTermContracts = await dbContext.ContractCurrents
            .Where(c => c.FixedTermLease == true)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, fixedTermContracts.Count);
        Assert.All(fixedTermContracts, c => Assert.True(c.FixedTermLease));
    }
}