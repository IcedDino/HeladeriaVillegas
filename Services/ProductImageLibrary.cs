using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public static class ProductImageLibrary
{
    public static IReadOnlyList<OpenverseImageResult> All { get; } =
    [
        Image("Papas / Sabritas", "product_papas.jpg", "Snacks", "papas sabritas chips bolsa", "chrisssss", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/56869573@N00/7983658161"),
        Image("Frituras / chicharrones", "product_frituras.jpg", "Snacks", "frituras chicharrones botana", "Calgary Reviews", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/51930963@N02/6514541865"),
        Image("Sopa instantánea", "product_sopa.jpg", "Snacks", "sopa ramen instantanea vaso", "elsie.hui", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/91188380@N05/9131988144"),
        Image("Palomitas", "product_palomitas.jpg", "Snacks", "palomitas popcorn", "Alex Munsell", "CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/", "https://stocksnap.io/photo/popcorn-bowl-885S4Q0UVA"),
        Image("Barquillo", "product_barquillo.jpg", "Helados", "barquillo cono helado", "TheCulinaryGeek", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/72949902@N00/5076899310"),
        Image("Vaso de helado", "product_vaso.jpg", "Helados", "vaso copa helado suave", "rachelkramerbussel.com", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/33108296@N07/3400808748"),
        Image("Canasta de helado", "product_canasta.jpg", "Helados", "canasta galleta helado", "bjacobdawson", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/145745023@N04/26047521477"),
        Image("Envase / bote", "product_envase.jpg", "Helados", "envase bote litro helado llevar", "Horacio Cambeiro", "BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/", "https://commons.wikimedia.org/w/index.php?curid=194082643"),
        Image("Malteada", "product_malteada.jpg", "Especialidades", "malteada batido fresa", "UniversityBlogSpot", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/90617638@N04/8356193238"),
        Image("Copa / sundae", "product_copa.jpg", "Especialidades", "copa sundae helado chocolate", "TheCulinaryGeek", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/72949902@N00/5076304681"),
        Image("Banana Split", "product_banana_split.jpg", "Especialidades", "banana split platano helado", null, "CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/", "https://www.rawpixel.com/image/5913643/image-public-domain-fruit-food"),
        Image("Tres Marías", "product_tres_marias.jpg", "Especialidades", "tres marias bolas helado", "Mohammed Kateregga", "CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/", "https://wordpress.org/photos/photo/46769d9265/"),
        Image("Nachos", "library_nachos.jpg", "Snacks", "nachos queso botana", "jeffreyw", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/7927684@N03/7237655232"),
        Image("Elote en vaso", "library_elote.jpg", "Snacks", "elote esquites maiz vaso", "DianaMoon", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/7752522@N02/16969943115"),
        Image("Churros", "library_churros.jpg", "Snacks", "churros chocolate postre", "avlxyz", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/10559879@N00/348229335"),
        Image("Brownie con helado", "library_brownie.jpg", "Especialidades", "brownie chocolate helado", "TheHungryDudes", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/47854142@N04/7315402166"),
        Image("Crepa", "library_crepa.jpg", "Especialidades", "crepa crepe postre", "TheGirlsNY", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/55768440@N00/7812564820"),
        Image("Frappé", "library_frappe.jpg", "Especialidades", "frappe cafe bebida", "Horia Varlan", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/10361931@N06/4269028564"),
        Image("Smoothie", "library_smoothie.jpg", "Especialidades", "smoothie licuado fruta", "José Carlos Cortizo Pérez", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/80272747@N00/4538722294"),
        Image("Refresco", "library_refresco.jpg", "Snacks", "refresco soda botella", "Joelk75", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/75001512@N00/3804078768"),
        Image("Agua embotellada", "library_agua.jpg", "Snacks", "agua botella", "Muffet", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/53133240@N00/7985698964"),
        Image("Rebanada de pastel", "library_pastel.jpg", "Especialidades", "pastel torta rebanada chocolate", "jeffreyw", "BY 2.0", "https://creativecommons.org/licenses/by/2.0/", "https://www.flickr.com/photos/7927684@N03/14940845945"),
        Image("Galletas", "library_galletas.jpg", "Snacks", "galletas cookies", "stu_spivack", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/35034346243@N01/3469840014"),
        Image("Waffle con helado", "library_waffle.jpg", "Especialidades", "waffle gofre helado postre", "stu_spivack", "BY-SA 2.0", "https://creativecommons.org/licenses/by-sa/2.0/", "https://www.flickr.com/photos/35034346243@N01/8712429852")
    ];

    public static IReadOnlyList<OpenverseImageResult> Find(string? category, string? search = null)
    {
        IEnumerable<OpenverseImageResult> images = All;

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            images = images.Where(image => image.LibraryCategory == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            images = images.Where(image =>
                image.DisplayTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                image.SearchKeywords.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return images.ToList();
    }

    public static OpenverseImageResult ForFile(string fileName) =>
        All.First(image => image.Thumbnail.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private static OpenverseImageResult Image(
        string title,
        string file,
        string category,
        string keywords,
        string? creator,
        string license,
        string licenseUrl,
        string sourceUrl) => new()
        {
            Id = file,
            Title = title,
            Thumbnail = file,
            Creator = creator,
            License = license,
            LicenseVersion = string.Empty,
            LicenseUrl = licenseUrl,
            SourceUrl = sourceUrl,
            LibraryCategory = category,
            SearchKeywords = keywords
        };
}
