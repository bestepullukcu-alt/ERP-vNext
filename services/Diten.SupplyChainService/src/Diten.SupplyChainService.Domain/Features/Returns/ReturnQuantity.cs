using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Diten.SupplyChainService.Domain.Features.Returns;
/// <summary>Exact decimal coefficient/scale arithmetic. Storage retains original text separately.</summary>
public readonly struct ReturnQuantity : IComparable<ReturnQuantity>, IEquatable<ReturnQuantity>
{
 private readonly BigInteger coefficient;
 private readonly int scale;
 private ReturnQuantity(BigInteger c,int s) { while(s>0 && c%10==0) {c/=10;s--;} coefficient=c;scale=s; }
 public static ReturnQuantity Zero => new(BigInteger.Zero,0);
 public static ReturnQuantity Parse(string text)
 {
  if(!Regex.IsMatch(text,@"\A-?[0-9]+(?:\.[0-9]+)?\z")) throw new FormatException("Invalid exact decimal");
  var dot=text.IndexOf('.');return new(BigInteger.Parse(text.Replace(".",""),CultureInfo.InvariantCulture),dot<0?0:text.Length-dot-1);
 }
 public static bool TryParse(string? text,out ReturnQuantity value) {value=Zero; if(text is null)return false;try {value=Parse(text);return true;}catch(FormatException){return false;}}
 public int CompareTo(ReturnQuantity other) {var s=Math.Max(scale,other.scale);return (coefficient*BigInteger.Pow(10,s-scale)).CompareTo(other.coefficient*BigInteger.Pow(10,s-other.scale));}
 public static ReturnQuantity operator +(ReturnQuantity a,ReturnQuantity b) {var s=Math.Max(a.scale,b.scale);return new(a.coefficient*BigInteger.Pow(10,s-a.scale)+b.coefficient*BigInteger.Pow(10,s-b.scale),s);}
 public static ReturnQuantity operator -(ReturnQuantity a,ReturnQuantity b) {var s=Math.Max(a.scale,b.scale);return new(a.coefficient*BigInteger.Pow(10,s-a.scale)-b.coefficient*BigInteger.Pow(10,s-b.scale),s);}
 public bool Equals(ReturnQuantity other)=>CompareTo(other)==0;
 public override bool Equals(object? obj)=>obj is ReturnQuantity q&&Equals(q);
 public override int GetHashCode()=>HashCode.Combine(coefficient,scale);
 public override string ToString() {var digits=BigInteger.Abs(coefficient).ToString(CultureInfo.InvariantCulture).PadLeft(scale+1,'0');return (coefficient.Sign<0?"-":"")+(scale==0?digits:digits.Insert(digits.Length-scale,"."));}
}
public static class ReturnInstant
{
 public static string Normalize(string text)
 {
  var m=Regex.Match(text,@"\A([0-9]{4}-[0-9]{2}-[0-9]{2})[Tt]([0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.([0-9]+))?([Zz]|[+-][0-9]{2}:[0-9]{2})\z");
  if(!m.Success)throw new FormatException("Invalid instant");
  var fraction=m.Groups[3].Value.TrimEnd('0');
  // Gregorian arithmetic includes the RFC 3339 four-digit year 0000.
  var date=m.Groups[1].Value;var clock=m.Groups[2].Value;
  var year=int.Parse(date.AsSpan(0,4),CultureInfo.InvariantCulture);
  var month=int.Parse(date.AsSpan(5,2),CultureInfo.InvariantCulture);
  var day=int.Parse(date.AsSpan(8,2),CultureInfo.InvariantCulture);
  var hour=int.Parse(clock.AsSpan(0,2),CultureInfo.InvariantCulture);
  var minute=int.Parse(clock.AsSpan(3,2),CultureInfo.InvariantCulture);
  var second=int.Parse(clock.AsSpan(6,2),CultureInfo.InvariantCulture);
  if(month<1||month>12||day<1||day>DaysInMonth(year,month)||hour>23||minute>59||second>60)
   throw new FormatException("Invalid Gregorian date/time");
  var offset=m.Groups[4].Value;
  var totalMinutes=hour*60+minute;
  if(offset is not ("Z" or "z"))
  {
   var hours=int.Parse(offset.AsSpan(1,2),CultureInfo.InvariantCulture);var minutes=int.Parse(offset.AsSpan(4,2),CultureInfo.InvariantCulture);
   if(hours>23||minutes>59)throw new FormatException("Invalid offset");
   totalMinutes+=(offset[0]=='+'?-1:1)*(hours*60+minutes);
  }
  if(totalMinutes<0)
  {
   totalMinutes+=1440;day--;
   if(day==0){month--;if(month==0){month=12;year--;}day=DaysInMonth(year,month);}
  }
  else if(totalMinutes>=1440)
  {
   totalMinutes-=1440;day++;
   if(day>DaysInMonth(year,month)){day=1;month++;if(month==13){month=1;year++;}}
  }
  hour=totalMinutes/60;minute=totalMinutes%60;
  if(second==60&&(hour!=23||minute!=59||day!=DaysInMonth(year,month)))
   throw new FormatException("Leap second is not in the UTC end-of-month minute");
  // At the representable UTC-year boundary retain a deterministic valid offset spelling.
  // The instant remains exact without inventing a five-digit or negative wire year.
  if(year<0||year>9999)
  {
   var boundaryYear=year<0?0:9999;
   var boundaryMonth=year<0?1:12;
   var boundaryDay=year<0?1:31;
   var canonicalOffset=year<0?1440-totalMinutes:totalMinutes+1;
   var canonicalClock=year<0?"00:00":"23:59";
   return string.Create(CultureInfo.InvariantCulture,$"{boundaryYear:D4}-{boundaryMonth:D2}-{boundaryDay:D2}T{canonicalClock}:{second:D2}")+
    (fraction.Length==0?"":"."+fraction)+(year<0?"+":"-")+
    string.Create(CultureInfo.InvariantCulture,$"{canonicalOffset/60:D2}:{canonicalOffset%60:D2}");
  }
  return string.Create(CultureInfo.InvariantCulture,$"{year:D4}-{month:D2}-{day:D2}T{hour:D2}:{minute:D2}:{second:D2}")+(fraction.Length==0?"":"."+fraction)+"Z";
 }
 private static int DaysInMonth(int year,int month)=>month switch
 {2=>year%4==0&&(year%100!=0||year%400==0)?29:28,4 or 6 or 9 or 11=>30,_=>31};
}
