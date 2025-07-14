import 'package:dartz/dartz.dart';

import '../../../../../core/error/failures.dart';
import '../../repositories/cart_repository.dart';

class DeleteCartItemUseCase {
  final CartRepository repository;
  DeleteCartItemUseCase(this.repository);

  @override
  Future<Either<Failure, void>> call(String cartItemString) async {
    return await repository.deleteFormCart(cartItemString);
  }
}
