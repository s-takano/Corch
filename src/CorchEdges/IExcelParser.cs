using System.Data;

namespace CorchEdges;

public interface IExcelParser { (DataSet?, string?) Parse(byte[] bytes); }