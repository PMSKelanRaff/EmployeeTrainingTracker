using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class EmployeeItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsGroup { get; set; } = false;

    public override string ToString() => Name; // what shows in CLB
}
