using Microsoft.Extensions.Caching.Memory;
using Shared.ExternalServices.Enums;
using Shared.ExternalServices.Interfaces;
using Shared.ExternalServices.ViewModels.Response;
using Shared.Utilities.Helpers;

namespace Shared.ExternalServices.MockServices
{
    public class SapMockService : ISapService
    {
        private IMemoryCache _cache;

        public SapMockService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<SAPCustomerResponse> FindCustomer(string companyCode, string countryCode, string distributorNumber)
        {
            //List<SAPCustomerResponse> customers = new()
            //{
            //    new SAPCustomerResponse
            //    { 
            //        DistributorName = "Chukwuemeka & CO.", 
            //        EmailAddress = "emeka.chikwendu@verraki.com", 
            //        FirstName = "Chukwuemeka", 
            //        LastName = "Chikwendu", 
            //        PhoneNumber = "08167488828", 
            //        Status = SapAccountStatusEnum.Active.ToDescription(), 
            //        DistributorNumber = "00100", 
            //        CompanyCode = "CC00100", 
            //        CountryCode = "NG", 
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Blessing & CO.",
            //        EmailAddress = "blessing.johnson@verraki.com",
            //        FirstName = "Blessing",
            //        LastName = "Johnson",
            //        PhoneNumber = "07032224027",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00101",
            //        CompanyCode = "CC00101",
            //        CountryCode = "NG",
            //        AccountType = "Cash Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Joseph & CO.",
            //        EmailAddress = "joseph.olusegun@verraki.com",
            //        FirstName = "Joseph",
            //        LastName = "Olusegun",
            //        PhoneNumber = "08067041115",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00102",
            //        CompanyCode = "CC00102",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Fake User Company",
            //        EmailAddress = "fake.user@verraki.com",
            //        FirstName = "Taiwan",
            //        LastName = "Japan",
            //        PhoneNumber = "08078675432",
            //        Status = SapAccountStatusEnum.Blocked.ToDescription(),
            //        DistributorNumber = "00103",
            //        CompanyCode = "CC00103",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },

            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chika Enterprises",
            //        EmailAddress = "chika.ejiofor@verraki.com",
            //        FirstName = "Ejiofor",
            //        LastName = "Chikum",
            //        PhoneNumber = "08109658805",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00104",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Cash Customer"
            //    },

            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Dayo Ale & Sons",
            //        EmailAddress = "oladayo.ale@verraki.com",
            //        FirstName = "Ale",
            //        LastName = "Oladayo",
            //        PhoneNumber = "07031863168",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00105",
            //        CompanyCode = "DCP",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Fadax Limited",
            //        EmailAddress = "benjamin.fadina@verraki.com",
            //        FirstName = "Benjamin",
            //        LastName = "Fadina",
            //        PhoneNumber = "07051870773",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00106",
            //        CompanyCode = "DCP",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Augie Limited",
            //        EmailAddress = "augustus.nwangwu@verraki.com",
            //        FirstName = "Augustus",
            //        LastName = "Nwangwu",
            //        PhoneNumber = "08138373179",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00107",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Sefat Limited",
            //        EmailAddress = "oluwaseyi.fatunmole@verraki.com",
            //        FirstName = "Oluwaseyi",
            //        LastName = "Fatunmole",
            //        PhoneNumber = "08137330706",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00108",
            //        CompanyCode = "NASCOM",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "JMoss Limited",
            //        EmailAddress = "joshua.akinyoade@verraki.com",
            //        FirstName = "Joshua",
            //        LastName = "Akinyoade",
            //        PhoneNumber = "07064749673",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00109",
            //        CompanyCode = "NASCOM",
            //        CountryCode = "NG",
            //        AccountType = "Bank Guarantee"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Lima Limited",
            //        EmailAddress = "augustine.nwagboso@verraki.com",
            //        FirstName = "Augustine",
            //        LastName = "Nwagboso",
            //        PhoneNumber = "08027458472",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00110",
            //        CompanyCode = "NASCOM",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chikas Limited",
            //        EmailAddress = "chikahen1000@gmail.com",
            //        FirstName = "New",
            //        LastName = "Chika",
            //        PhoneNumber = "08027458409",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00111",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chi Lima Limited",
            //        EmailAddress = "chikaejiofor95@yahoo.com",
            //        FirstName = "Chima",
            //        LastName = "Lima",
            //        PhoneNumber = "08027458410",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00112",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Uchenna Limited",
            //        EmailAddress = "uchennaonouha92@gmail.com",
            //        FirstName = "Uchenna",
            //        LastName = "Onouha",
            //        PhoneNumber = "08027458234",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00113",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Okorie Limited",
            //        EmailAddress = "okoriesamuel253@gmail.com",
            //        FirstName = "Okorie",
            //        LastName = "Samuel",
            //        PhoneNumber = "08027458109",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00114",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "emailsender1771@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00115",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "augustusnwangwu@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00116",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "augustus.nwangwu@verraki.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00117",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "augustus.nwangwu@dangote.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00118",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "gwealthconcept@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00119",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "austusnobet@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460910",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00120",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "austusnobeto@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460913",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00121",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "austusnobeto1@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460990",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00122",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "austusnobeto2@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460930",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00123",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    },
            //    new SAPCustomerResponse
            //    {
            //        DistributorName = "Chimaz Lima Limited",
            //        EmailAddress = "austusnobeto3@gmail.com",
            //        FirstName = "Chimazu",
            //        LastName = "Limatiz",
            //        PhoneNumber = "08027460929",
            //        Status = SapAccountStatusEnum.Active.ToDescription(),
            //        DistributorNumber = "00124",
            //        CompanyCode = "DSR",
            //        CountryCode = "NG",
            //        AccountType = "Clean Credit Customer"
            //    }
            //};

            var customers = GetSapAccounts();
            if(!customers.Any())
                return null;

            var customer = customers.FirstOrDefault(x => x.DistributorNumber.Equals(distributorNumber, StringComparison.OrdinalIgnoreCase) && x.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase) && x.CompanyCode.Equals(companyCode, StringComparison.OrdinalIgnoreCase));
            return await Task.Run(() => customer);
        }

        private List<SAPCustomerResponse> GetSapAccounts()
        {
            var accountKey = "SapAccounts";

            if (_cache.TryGetValue(accountKey, out List<SAPCustomerResponse> cacheProducts))
                return cacheProducts;

            return new List<SAPCustomerResponse>();
        }
    }
}
