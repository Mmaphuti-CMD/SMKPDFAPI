# Transaction Count Analysis

## Manual Count from PDF Text:

1. 01/11/2025 Live Better Interest Sweep Transfer -0.16 91.50
2. 01/11/2025 Banking App Transfer to Short: Transfer Transfer -53.46 38.04
3. 01/11/2025 Payment Received: M Madiope Other Income 200.00 238.04
4. 01/11/2025 Banking App External Payment: King Rental Income -195.00 -2.00 41.04
5. 15/11/2025 Payment Received: Grocery Add Up Transfer 2796356853 Other Income 100.00 141.04
6. 15/11/2025 Banking App Transfer to Short: Transfer Transfer -100.00 41.04
7. 18/11/2025 Adjustment: Refund For Incorrect Decline Fee Other Income 4.00 45.04
8. 22/11/2025 Set-off Applied Transfer -1.00 44.04
9. 23/11/2025 PayShap Payment Received: Grocery Add Up Other Income 100.00 144.04
10. 23/11/2025 Banking App Immediate Payment: Nn Dlamini Digital Payments -50.00 -1.00 93.04
11. 23/11/2025 PayShap Payment Received: Grocery Add Up Other Income 500.00 593.04
12. 23/11/2025 Banking App Immediate Payment: Master Rey Digital Payments -500.00 -1.00 92.04
13. 23/11/2025 SMS Payment Notification Fee Fees -0.35 91.69
14. 25/11/2025 PayShap Payment Received: Grocery Add Up Other Income 505.00 596.69
15. 26/11/2025 Banking App Transfer to Short: Transfer Transfer -501.00 95.69
16. 28/11/2025 Payment Received: Grocery Add Up Transfer 2827425928 Other Income 450.00 545.69
17. 29/11/2025 Banking App Immediate Payment: Screen Digital Payments -75.00 -1.00 469.69
18. 30/11/2025 Banking App Transfer to Short: Transfer Transfer -400.00 69.69
19. 30/11/2025 Interest Received Interest 0.16 69.85
20. 30/11/2025 Monthly Account Admin Fee Fees -7.50 62.35
21. 01/12/2025 Live Better Interest Sweep Transfer -0.16 62.19
22. 01/12/2025 Payment Received: Grocery Add Up Transfer 2836650527 Other Income 600.00 662.19
23. 01/12/2025 Banking App Transfer to Short: Transfer Transfer -600.13 62.06
24. 02/12/2025 Insf. Funds Distrokid Musician New (INCOMPLETE - no amount)
25. 02/12/2025 International Online Purchase Insufficient Funds Fee: Distrokid Musician New York Us Fees -2.00 60.06
26. 06/12/2025 Banking App Transfer Received from Short: Transfer Transfer 1 000.00 1 060.06
27. 06/12/2025 PayShap Payment Received: Grocery Add Up Other Income 460.00 1 520.06
28. 08/12/2025 Rana General Trading P Witbank (Card 7938) Furniture & Appliances -1 000.00 520.06
29. 09/12/2025 Banking App Prepaid Purchase: Telkom Mobile Cellphone -8.00 -0.50 511.56
30. 12/12/2025 Online Purchase: Distrokid Musician New York (Card 7938) Digital Subscriptions -459.99 51.57
31. 13/12/2025 Live Better Round-up Transfer Transfer -0.01 51.56
32. 13/12/2025 International Online Purchase Insufficient Funds Fee: Cursor, Ai Powered Ide New York Us Fees -2.00 49.56
33. 15/12/2025 Payment Received: Master Ray Other Income 100.00 149.56
34. 16/12/2025 Banking App External PayShap Payment: King Digital Payments -100.00 -6.00 43.56

## Total: 34 transactions (but line 24 is incomplete)

## Issues Identified:

1. **Line 24 is incomplete**: "02/12/2025 Insf. Funds Distrokid Musician New" - This line has no monetary value, so it will be skipped by the parser (correct behavior)

2. **Multi-line transactions**: Some transactions span multiple lines in the PDF (like line 28-29 for "Rana General Trading" and line 30-31 for "Online Purchase: Distrokid")

3. **Expected count**: 33 valid transactions (34 total - 1 incomplete = 33)

4. **API returned**: 32 transactions

## Possible Reasons for Mismatch:

1. **Multi-line transaction parsing**: Transactions that span multiple lines might be:
   - Merged incorrectly
   - One line skipped
   - Parsed as separate incomplete transactions

2. **Duplicate detection**: Some transactions might be incorrectly identified as duplicates

3. **Parser skipping valid transactions**: Some transactions might not match the regex pattern

