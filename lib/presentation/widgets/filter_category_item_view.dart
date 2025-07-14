import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/entities/category/category.dart';
import '../blocs/filter/filter_cubit.dart';

class FilterCategoryItemView extends StatelessWidget {
  final Category mainCategory;
  final List<Category> selectedCategories;

  const FilterCategoryItemView(
      {super.key,
      required this.mainCategory,
      required this.selectedCategories});

  @override
  Widget build(BuildContext context) {
    bool isSelected = isMainCategorySelected();
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        categoryItem(
            category: mainCategory,
            isMainCategory: true,
            onTap: () {
              if (mainCategory.subcategories != null &&
                  mainCategory.subcategories!.isNotEmpty) {
                for (var entry in mainCategory.subcategories!.entries) {
                  context
                      .read<FilterCubit>()
                      .updateCategory(category: entry.value);
                }
                context
                    .read<FilterCubit>()
                    .updateCategory(category: mainCategory);
              } else {
                context
                    .read<FilterCubit>()
                    .updateCategory(category: mainCategory);
              }
            },
            isSelected: isSelected),
        if (mainCategory.subcategories != null &&
            mainCategory.subcategories!.isNotEmpty) ...[
          ...mainCategory.subcategories!.entries.map((entry) => Row(
                children: [
                  const SizedBox(
                    width: 8,
                  ),
                  Expanded(
                    child: categoryItem(
                        category: entry.value,
                        onTap: () {
                          context
                              .read<FilterCubit>()
                              .updateCategory(category: entry.value);
                        },
                        isSelected: selectedCategories
                            .any((e) => e.id == entry.value.id)),
                  )
                ],
              ))
        ],
      ],
    );

    InkWell(
      onTap: () {
        /*context.read<FilterCubit>().updateCategory(
            category: categoryState.categories[index]);*/
      },
      child: Row(
        children: [
          Text(
            mainCategory.name,
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w500),
          ),
          const Spacer(),
          /*BlocBuilder<FilterCubit, FilterProductParams>(
            builder: (context, filterState) {
              return Checkbox(
                value: filterState.categories.contains(
                    categoryState.categories[index]) ||
                    filterState.categories.isEmpty,
                onChanged: (bool? value) {
                  context.read<FilterCubit>().updateCategory(
                      category: categoryState.categories[index]);
                },
              );
            },
          )*/
        ],
      ),
    );
  }

  bool isMainCategorySelected() {
    if (mainCategory.subcategories != null &&
        mainCategory.subcategories!.isNotEmpty) {
      for (var entry in mainCategory.subcategories!.entries) {
        if (!selectedCategories
            .any((selectedCategory) => selectedCategory.id == entry.value.id)) {
          return false;
        }
      }
      return true;
    } else {
      return selectedCategories
          .any((selectedCategory) => selectedCategory.id == mainCategory.id);
    }
  }

  Widget categoryItem(
      {required Category category,
      required VoidCallback onTap,
      required bool isSelected,
      bool isMainCategory = false}) {
    return InkWell(
      onTap: onTap,
      child: Row(
        children: [
          Expanded(
            child: Text(
              category.name,
              style: TextStyle(
                  fontSize: isMainCategory ? 16 : 14,
                  fontWeight:
                      isMainCategory ? FontWeight.w600 : FontWeight.w400),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          Checkbox(
            value: isSelected,
            onChanged: (bool? value) {
              onTap();
            },
          )
        ],
      ),
    );
  }
}
