import 'package:dartz/dartz.dart';

import '../../../../../core/error/failures.dart';
import '../../../../../core/usecases/usecase.dart';
import '../../entities/category/category.dart';
import '../../entities/product/product_response.dart';
import '../../repositories/product_repository.dart';

class GetProductUseCase
    implements UseCase<ProductResponse, FilterProductParams> {
  final ProductRepository repository;
  GetProductUseCase(this.repository);

  @override
  Future<Either<Failure, ProductResponse>> call(
      FilterProductParams params) async {
    return await repository.getProducts(params);
  }
}

class FilterProductParams {
  final String? keyword;
  final List<Category> categories;
  final List<Category> subCategories;
  final double minPrice;
  final double maxPrice;
  final int? limit;
  final int? pageSize;

  const FilterProductParams({
    this.keyword = '',
    this.categories = const [],
    this.subCategories = const [],
    this.minPrice = 0,
    this.maxPrice = 1000000,
    this.limit = 0,
    this.pageSize = 10,
  });

  FilterProductParams copyWith({
    int? skip,
    String? keyword,
    List<Category>? categories,
    List<Category>? subcategories,
    double? minPrice,
    double? maxPrice,
    int? limit,
    int? pageSize,
  }) =>
      FilterProductParams(
        keyword: keyword ?? this.keyword,
        categories: categories ?? this.categories,
        subCategories: subcategories ?? this.subCategories,
        minPrice: minPrice ?? this.minPrice,
        maxPrice: maxPrice ?? this.maxPrice,
        limit: skip ?? this.limit,
        pageSize: pageSize ?? this.pageSize,
      );
}
