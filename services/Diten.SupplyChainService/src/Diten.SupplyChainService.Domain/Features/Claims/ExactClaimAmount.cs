using System.Text.RegularExpressions;
namespace Diten.SupplyChainService.Domain.Features.Claims;
/// <summary>Exact decimal comparison without floating point or fixed precision; original wire text remains storage value.</summary>
public sealed class ExactClaimAmount : IComparable<ExactClaimAmount>
{
 public string Original {get;}
 private readonly string whole;
 private readonly string fraction;
 public bool IsNegative {get;}
 public bool IsPositive {get;}
 private ExactClaimAmount(string text)
 {
  Original=text; var digits=text.TrimStart('-').Split('.'); whole=digits[0].TrimStart('0'); if(whole.Length==0)whole="0";
  fraction=digits.Length==2?digits[1].TrimEnd('0'):"";
  var zero=whole=="0"&&fraction.Length==0; IsNegative=text[0]=='-'&&!zero; IsPositive=!IsNegative&&!zero;
 }
 public static bool TryParse(string? text,out ExactClaimAmount? amount)
 { amount=null; if(text is null||!Regex.IsMatch(text,@"\A-?[0-9]+(?:\.[0-9]+)?\z"))return false; amount=new(text);return true; }
 public static ExactClaimAmount Parse(string text)=>TryParse(text,out var a)?a!:throw new FormatException("Invalid exact decimal.");
 public int CompareTo(ExactClaimAmount? other)
 {
  if(other is null)return 1; if(IsNegative!=other.IsNegative)return IsNegative?-1:1;
  var cmp=whole.Length.CompareTo(other.whole.Length);if(cmp==0)cmp=string.CompareOrdinal(whole,other.whole);
  if(cmp==0)for(var i=0;i<Math.Max(fraction.Length,other.fraction.Length);i++){cmp=(i<fraction.Length?fraction[i]:'0').CompareTo(i<other.fraction.Length?other.fraction[i]:'0');if(cmp!=0)break;}
  return IsNegative?-cmp:cmp;
 }
}
