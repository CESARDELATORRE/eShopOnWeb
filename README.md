## Create and Use the ML.NET model

eShopOnWebML app comes with a previously trained model for product recommendation, but you can train your own model based in your own data. 

The console application project `ProductRecommendation.Train` can be used to generate the product recommendation model. You need to follow next steps in order to generate these models:

1) **Set VS default startup project:** Set `ProductRecommendation.Train` as starting project in Visual Studio
2) **Run the training model console app:** Hit F5 in Visual Studio. At the end of the execution, the output will be similar to this screenshot:
![image](/docs/images/train_console.png)
3) **Copy the model file into the Infrastructure project:** By default, when the training finishes, the model is saved at `src / ProductRecommendation.Train / assets / output / productRecommendation.zip`. Copy this file into  `src / Infrastructure / Setup / `[model](https://github.com/CESARDELATORRE/eShopOnWeb/tree/master/src/Infrastructure/Setup/model) .
4) **[Optional] Generate your own input training data:** The folder `assets/inputs` inside the project `ProductRecommendation.Train` contains default training file  `orderItems.csv`. This file needs to be preprocess first, in order to be consumed later by the training application. In order to execute the preprocessing, comment out following line at `Program.cs` :
```csharp
modelBuilder.PreProcess(salesOriginalCsv);
``` 
After executing the preprocessing step, a file name `orderItemsPre.csv` will be generated. This file contains the data arranged in 4 columns: CustomerId, ProductId, Quantity and Recommendation. 

|CustomerId|ProductId|Quantity|Label|
|----------|---------|--------|-----|
|1d211951-7593-4828-9e46-1e1c56afa74a|100|12|0|
|64ea8383-b9b1-43fb-b5df-93498df69b40|100|48|1|
|209ace0e-b07b-4f6b-9b8d-65f46b3213ef|100|12|0|
|78a64378-f69f-4934-b0af-94ad3c878276|100|12|0|

## Code Walkthrough

### ML.NET: Model creation
The model training source code is located at `src / ProductRecommentation.Train / Model / `[ModelBuilder.cs](https://github.com/CESARDELATORRE/eShopOnWeb/blob/master/src/ProductRecommendation.Train/Model/ModelBuilder.cs).

The beginning of the machine learning pipeline starts defining the data source. In this case, it will be based on text (which will be stored in files), and `TextLoader.CreateReader()` is used to define a generic loader.
```csharp
var reader = TextLoader.CreateReader(env,
                c => (
                    CustomerId: c.LoadText(0),
                    ProductId: c.LoadText(1),
                    Quantity: c.LoadFloat(2),
                    Label: c.LoadBool(3)),
                separator: ',', hasHeader: true);
```
The schema defined in the loader follows the schema the the text must follow: lines, in which each field is separated by commas and with a default header in the first line, such as in this example:
```csv
CustomerId,ProductId,Quantity,Label
1d211951-7593-4828-9e46-1e1c56afa74a,100,12,0
64ea8383-b9b1-43fb-b5df-93498df69b40,100,48,1
209ace0e-b07b-4f6b-9b8d-65f46b3213ef,100,12,0
78a64378-f69f-4934-b0af-94ad3c878276,100,12,0
```

Then, we define the estimator chain, implemented as:
```csharp
var est = reader.MakeNewEstimator()
    .Append(row => (CustomerId_OHE: row.CustomerId.OneHotEncoding(), ProductId_OHE: row.ProductId.OneHotEncoding(), row.Label))
    .Append(row => (Features: row.CustomerId_OHE.ConcatWith(row.ProductId_OHE), row.Label))
    .Append(row => (row.Label, 
    preds: ctx.Trainers.FieldAwareFactorizationMachine(
        row.Label, 
        new[] { row.Features }, 
        advancedSettings: ffmArguments => ffmArguments.Shuffle = false,
        onFit: p => pred = p)));

var pipe = reader.Append(est);
```

The estimation pipe is supported by what is called the **Static API**. Using this API, transformers and learners are applied  naturally as extensions method to current types, making more easy to discover the API using strong types:
* `.MakeEstimator()`: Create an estimator pipe.
* `.OneHotEncoding()`: CustomerId and ProductId are transformed using [One Hot Encoding](https://en.wikipedia.org/wiki/One-hot).
* `.ConcatWith()`: Data needs to be combined into a single column (named `Features`) as a prior step before the learner starts executing.
* `.FieldAwareFactorizationMachine()`: The learner used by the pipeline is called [Field-aware Factorization Machine](https://github.com/wschin/fast-ffm/blob/master/fast-ffm.pdf), this algorithm evaluates the interaction between several features (in our case, CustomerId and ProductId), and it can be used with [sparse data](https://en.wikipedia.org/wiki/Sparse_matrix).

After building the pipeline, we train the recommendation model using the training file:
```csharp
var dataSource = new MultiFileSource(orderItemsLocation);
var model = pipe.Fit(dataSource);
```

Additionally, we evaluate the accuracy of the model. This accuracy is measured using the `BinaryClassificationContext`, and the [Accuracy](https://en.wikipedia.org/wiki/Confusion_matrix) and [AUC](https://loneharoon.wordpress.com/2016/08/17/area-under-the-curve-auc-a-performance-metric/) metrics are displayed.
```csharp
var metrics = ctx.Evaluate(data, r => r.Label, r => r.preds);
```

### ML.NET: Model Prediction
The model created in former step, is used to make recommendations for users. When the user logs in the website, his homepage will display first recommended products for him/her, based on previous purchases.
The source code of prediction core is in `src / Infrastructure / Services / `[ProductRecommendationService.cs](https://github.com/CESARDELATORRE/eShopOnWeb/blob/master/src/Infrastructure/Services/ProductRecommendationService.cs), inside the method `GetRecommendationsForUserAsync()`.

```csharp
public async System.Threading.Tasks.Task<IEnumerable<string>> GetRecommendationsForUserAsync
    (string user, string[] products, int recommendationsInPage)
{
var model = LoadModel(modelLocation);

// Create all possible SalesData objects between (unique) CustomerId x ProductId (many)
var crossUserProducts = from product in products
                        select new SalesData { CustomerId = user, ProductId = product };

// Execute the recommendation model with previous generated data
var predictions = crossUserProducts
    .Select(crossUserProduct => model.Predict(crossUserProduct))
    .ToArray();

//Count how many recommended products the user gets (with more or less score..)
var numberOfRecommendedProducts = predictions.Where(x => x.Recommendation).Select(x => x.Recommendation).Count();

//Count how many recommended products the user gets (with more than 0.7 score..)
var RecommendedProductsOverThreshold = (from p in predictions
                                        orderby p.Score descending
                                        where p.Recommendation && p.Score > 0.7
                                        select new SalesPrediction { ProductId = p.ProductId, Score = p.Score, Recommendation = p.Recommendation });

var numberOfRecommendedProductsOverThreshold = RecommendedProductsOverThreshold.Count();

// Return (recommendationsInPage) product Ids ordered by Score
return predictions
    .Where(p => p.Recommendation)
    .OrderByDescending(p => p.Score)
    .Select(p => p.ProductId)
    .Take(recommendationsInPage);
}
```

The method receives as parameters the user and the products we need to check for posible recommendations. The method then creates `SalesData` objects (one per product / customer). The model returns the probability and the label (recommended / not recommended), so the method returns only recommended predictions, ordered by probability and only the first ones (taken `recommendationsInPage` predictions).

### [optional] Model pre-processing 
Before creating the model, we need to pre-process the original input data. This data is contained by the file `assets/inputs/orderItems.csv`.  The reason behind this is because we will use a method that is able to make only binary recommendations, and our Label feature (quantity) is a continuos variable. The pre-process will transform this continuous variable into a categorical variable with 2 states: recommend / not recommend (true / false).

There are several methods for discretizing a continuous variable, in this case we will set a threshold, and then we will transform values over or equal the threshold to true (do recommend), otherwise, to false (do not recommend). In this case, the mean by product is used as a threshold. 

Previous transformation is supported by the method `PreProcess()`. As result, we will add one column named `Recommend` holding Quantity as a discretized value (true / false). The pre-processed dataset will be saved as `assets/inputs/orderItemsPre.csv`.


## Run the web app with the recommendations

When running the web app, in order to see the recommendations, you first need to authenticate with a demo user with these credentials:

User: demouser@microsoft.com

Password: Pass@word1

The app runs generates the recommendations for that particular user (based on his orders history compared to other orders from other users) by using the ML.NET model and shows the first 6 recommendations on top of the regular product catalog, like in the following screenshot:

![image](https://user-images.githubusercontent.com/1712635/45646295-bc6dff00-ba77-11e8-8dd8-e8417c309a8c.png)

## Running the web app with SQL Server hosting the database instead of In Memory database

After cloning or downloading the web app sample, you should be able to run it using an In Memory database, immediately. That database is used for handling the Product Catalog and other typical entities. If you wish to use the sample with a persistent SQL Server database, you will need to modify the setup as explained in the original eShopOnWeb repo, here: https://github.com/dotnet-architecture/eShopOnWeb 

## Citation
eShopOnWeb dataset is based on a public Online Retail Dataset from **UCI**: http://archive.ics.uci.edu/ml/datasets/online+retail
> Daqing Chen, Sai Liang Sain, and Kun Guo, Data mining for the online retail industry: A case study of RFM model-based customer segmentation using data mining, Journal of Database Marketing and Customer Strategy Management, Vol. 19, No. 3, pp. 197â€“208, 2012 (Published online before print: 27 August 2012. doi: 10.1057/dbm.2012.17).

