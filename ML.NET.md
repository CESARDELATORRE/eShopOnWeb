## Create and Use the model

eShopOnWeb app comes with a previously trained model for product recommendation, but you can train your own model based in your own data. 

The console application project `ProductRecommendation.Train` can be used to generate the product recommendation model. You need to follow next steps in order to generate these models:

1) **Set VS default startup project:** Set `ProductRecommendation.Train` as starting project in Visual Studio
2) **(Optional) - Generate your own input training data:** The `assets/inputs` folder contains default training file  `orderItems.csv`. This file contains the data arranged in 3 columns: CustomerId, ProductId and Quantity. If you want to use your own training file, you should follow the same schema and replace current default training file.
3) **Run the training model console app:** Hit F5 in Visual Studio. At the end of the execution, the output will be similar to this screenshot:
![image](/docs/images/train_console.png)
4) **Copy the model file into the Infrastructure project:** By default, when the execution finishes, the model is saved at `assets/output/productRecommendation.zip`. Copy model file into  `src / Infrastructure / Setup / `[model](https://github.com/CESARDELATORRE/eShopOnWeb/tree/master/src/Infrastructure/Setup/model) using the same name.

## Code Walkthrough

### ML.NET: Model creation
The model training source code is located at `src / ProductRecommentation.Train / Model / `[ModelBuilder.cs](https://github.com/CESARDELATORRE/eShopOnWeb/blob/master/src/ProductRecommendation.Train/Model/ModelBuilder.cs).

Before creating the model, in this case we need to pre-process the input data. The reason behind this is because we will use a method that is able to make only binary recommendations, and our Label feature (quantity) is a continuos variable. The pre-process will transform this continuous variable into a categorical variable with 2 states: recommend / not recommend (true / false).

There are several methods for discretizing a continuous variable, in this case we will set a threshold, and then we will transform values over or equal the threshold to true (do recommend), otherwise, to false (do not recommend). Finally, the mean by product is used as a threshold. 

Previous transformation is supported by the method `PreProcess()`. As result, we will add one column named `Recommend` holding the quantity discretized value (true / false).

```csharp
var pipeline = new LearningPipeline();

pipeline.Add(CollectionDataSource.Create(salesData));

pipeline.Add(new CategoricalHashOneHotVectorizer(
  (nameof(SalesRecommendationData.ProductId), 
  nameof(SalesRecommendationData.ProductId) + "_OH")) { HashBits = 18 });
pipeline.Add(new CategoricalHashOneHotVectorizer(
  (nameof(SalesRecommendationData.CustomerId), 
  nameof(SalesRecommendationData.CustomerId) + "_OH")) { HashBits = 18 });

pipeline.Add(new ColumnConcatenator("Features", 
nameof(SalesRecommendationData.ProductId) + "_OH",
nameof(SalesRecommendationData.CustomerId) + "_OH"));

pipeline.Add(new FieldAwareFactorizationMachineBinaryClassifier() { LearningRate = 0.05F, Iters = 1, LambdaLinear = 0.0002F });
```

The training pipeline is supported by the following components:
* [CollectionDataSource.Create](https://docs.microsoft.com/en-gb/dotnet/api/microsoft.ml.data.collectiondatasource.create?view=ml-dotnet#Microsoft_ML_Data_CollectionDataSource_Create__1_System_Collections_Generic_IEnumerable___0__): The preprocessed data can be directly use as input for the pipeline.
* [CategoricalHashOneHotVectorizer](https://docs.microsoft.com/en-gb/dotnet/api/microsoft.ml.transforms.categoricalhashonehotvectorizer?view=ml-dotnet): CustomerId and ProductId are transformed using a One Hot Encoding variant based on hashing
* [ColumnConcatenator](https://docs.microsoft.com/en-gb/dotnet/api/microsoft.ml.transforms.columnconcatenator?view=ml-dotnet): Data needs to be combined into a single column (by default, named `Features`) as a prior step before the learner.
* [FieldAwareFactorizationMachineBinaryClassifier](https://docs.microsoft.com/en-gb/dotnet/api/microsoft.ml.trainers.fieldawarefactorizationmachinebinaryclassifier?view=ml-dotnet): The learner used by the pipeline, this algorithm evaluates the interaction between CustomerId and ProductId, and can be used with [sparse data](https://en.wikipedia.org/wiki/Sparse_matrix).

After building the pipeline, we train the recommendation model:
```csharp
var model = learningPipeline.Train<SalesData, SalesPrediction>();
```

Finally, we save the recommendation model to local disk:
```csharp
await model.WriteAsync(modelLocation);
```

Additionally, we evaluate the accuracy of the model. This accuracy is measured using the [BinaryClassificationEvaluator](https://docs.microsoft.com/en-gb/dotnet/api/microsoft.ml.models.binaryclassificationevaluator?view=ml-dotnet), and the [Accuracy](https://en.wikipedia.org/wiki/Confusion_matrix) and [AUC](https://loneharoon.wordpress.com/2016/08/17/area-under-the-curve-auc-a-performance-metric/) metrics are displayed.

### ML.NET Model Prediction
The model created in former step, is used to make recommendations for users. When the user logs in the website, his homepage will display first recommended products for him/her, based on previous purchases.
The source code of prediction core is in `src / Infrastructure / Services / `[ProductRecommendationService.cs](https://github.com/CESARDELATORRE/eShopOnWeb/blob/master/src/Infrastructure/Services/ProductRecommendationService.cs), inside the method `GetRecommendationsForUserAsync()`.

```csharp
public async System.Threading.Tasks.Task<IEnumerable<string>> GetRecommendationsForUserAsync
    (string user, string[] products, int recommendationsInPage)
{
    var model = await PredictionModel.ReadAsync<SalesData, SalesPrediction>(modelLocation);
    var crossPredictions = from product in products                                   
                            select new SalesData { CustomerId = user, ProductId = product };

    var predictions = model.Predict(crossPredictions).ToArray();

    return predictions.Where(p => p.Recommendation.IsTrue)
        .OrderByDescending(p => p.Probability)
        .Select(p => p.ProductId)
        .Take(recommendationsInPage);
}
```

The method receives as parameters the user and the products we need to check. The method then creates `SalesData` objects (one object per product received as parameter, using always the same customer). The model returns the probability and the label (recommended / not recommended), so the method returns only recommended predictions, ordered by probability and only the first ones (taken `recommendationsInPage` predictions).

When running the web app, and after authenticating the user with these credentials:
User: demouser@microosft.com
Password: Pass@word1

The app runs generates the recommendations by using the ML.NET model and shows the first 6 recommendations on top of the regular product catalog, like in the following screenshot:

![image](https://user-images.githubusercontent.com/1712635/45646295-bc6dff00-ba77-11e8-8dd8-e8417c309a8c.png)

## Citation
eShopOnWeb dataset is based on a public Online Retail Dataset from **UCI**: http://archive.ics.uci.edu/ml/datasets/online+retail
> Daqing Chen, Sai Liang Sain, and Kun Guo, Data mining for the online retail industry: A case study of RFM model-based customer segmentation using data mining, Journal of Database Marketing and Customer Strategy Management, Vol. 19, No. 3, pp. 197â€“208, 2012 (Published online before print: 27 August 2012. doi: 10.1057/dbm.2012.17).

