namespace DemoRepo;

/// <summary>
/// Intentionally bad code for AI Refactor demos.
/// Smells: deeply nested if/else, magic numbers, repeated condition checks,
/// no early returns, integer "status" codes instead of an enum, parameter not
/// validated, side-effecting Console writes inside business logic.
///
/// Expected LLM refactor: guard clauses + early returns, extract magic numbers
/// into named constants, introduce a DiscountResult record/enum, split
/// presentation (Console) from calculation, add nullable-ref-friendly checks.
/// </summary>
public class OrderProcessor
{
    public int ApplyDiscount(string customerTier, decimal orderTotal, int itemCount, bool isReturningCustomer)
    {
        int finalDiscount = 0;
        if (customerTier == "gold")
        {
            if (orderTotal > 1000)
            {
                if (itemCount >= 5)
                {
                    if (isReturningCustomer)
                    {
                        finalDiscount = 25;
                        System.Console.WriteLine("Granting 25% gold-returning bulk discount");
                    }
                    else
                    {
                        finalDiscount = 20;
                        System.Console.WriteLine("Granting 20% gold bulk discount");
                    }
                }
                else
                {
                    if (isReturningCustomer)
                    {
                        finalDiscount = 18;
                        System.Console.WriteLine("Granting 18% gold returning discount");
                    }
                    else
                    {
                        finalDiscount = 15;
                        System.Console.WriteLine("Granting 15% gold discount");
                    }
                }
            }
            else
            {
                finalDiscount = 10;
                System.Console.WriteLine("Granting 10% gold base discount");
            }
        }
        else if (customerTier == "silver")
        {
            if (orderTotal > 500 && itemCount >= 3)
            {
                finalDiscount = 12;
                System.Console.WriteLine("Granting 12% silver bulk discount");
            }
            else
            {
                finalDiscount = 5;
                System.Console.WriteLine("Granting 5% silver base discount");
            }
        }
        else if (customerTier == "bronze")
        {
            if (orderTotal > 200)
            {
                finalDiscount = 3;
                System.Console.WriteLine("Granting 3% bronze discount");
            }
        }
        else
        {
            finalDiscount = 0;
            System.Console.WriteLine("No discount for unknown tier");
        }
        return finalDiscount;
    }
}
